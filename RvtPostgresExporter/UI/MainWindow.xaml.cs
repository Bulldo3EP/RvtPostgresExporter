using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Npgsql; // важно: добавить ссылку на Npgsql в проект
using RvtPostgresExporter.Database;
using RvtPostgresExporter.Export;
using RvtPostgresExporter.Revit;

namespace RvtPostgresExporter.UI
{
    public partial class MainWindow : Window
    {
        private readonly UIDocument _uiDoc;

        private string _profilePath;
        private ConnectionsConfig _connectionsCfg;
        private DbConnectionConfig _currentDb;

        public MainWindow(UIDocument uiDoc)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            InitializeComponent();

            BtnExportTxt.IsEnabled = false;
            BtnExportPg.IsEnabled = false;
        }

        // 1) Загрузка профиля parameters.json
        private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Выберите parameters.json"
            };

            if (dlg.ShowDialog(this) != true)
                return;

            _profilePath = dlg.FileName;
            TxtProfileInfo.Text = $"Профиль: {Path.GetFileName(_profilePath)}";
            TxtStatus.Text = "Профиль выбран. Можно экспортировать TXT. Для БД выберите подключение.";

            BtnExportTxt.IsEnabled = true;
            BtnExportPg.IsEnabled = _currentDb != null;
        }

        // 2) Выбор подключения к DB
        private async void BtnSelectDb_Click(object sender, RoutedEventArgs e)
        {
            await RunBusy(async () =>
            {
                var path = GetDefaultConnectionsPath();
                var loader = new ConnectionsLoader();
                var load = loader.Load(path);

                if (!load.IsValid)
                {
                    Dispatcher.Invoke(() =>
                        MessageBox.Show(this, string.Join("\n", load.Errors), "connections.json error",
                            MessageBoxButton.OK, MessageBoxImage.Error));
                    return;
                }

                _connectionsCfg = load.Config;

                Dispatcher.Invoke(() =>
                {
                    var sel = new ConnectionSelectorWindow { Owner = this };
                    sel.LoadFromConnectionsConfig(_connectionsCfg);

                    if (sel.ShowDialog() == true)
                    {
                        try
                        {
                            var resolver = new ConnectionResolver();
                            _currentDb = resolver.Resolve(_connectionsCfg, sel.SelectedConnectionName);

                            TxtDbStatus.Text = $"Выбрано: {_currentDb.Name} ({_currentDb.Host}:{_currentDb.Port}/{_currentDb.Database})";
                            TxtStatus.Text = "Подключение выбрано. Нажмите 'Проверить подключение' или экспортируйте.";
                            BtnExportPg.IsEnabled = !string.IsNullOrWhiteSpace(_profilePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                });

                await Task.CompletedTask;
            });
        }

        // 3) Проверка подключения к DB (как было)
        private async void BtnCheckDb_Click(object sender, RoutedEventArgs e)
        {
            await RunBusy(async () =>
            {
                if (_currentDb == null)
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Сначала выберите подключение (connections.json).");
                    return;
                }

                var pg = new PostgresService(_currentDb);
                bool ok = await pg.CheckConnectionAsync().ConfigureAwait(false);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = ok ? "Подключение к PostgreSQL: OK" : "Подключение к PostgreSQL: FAIL";
                });
            });
        }

        // 4) Экспорт TXT (как было)
        private async void BtnExportTxt_Click(object sender, RoutedEventArgs e)
        {
            await RunBusy(async () =>
            {
                if (string.IsNullOrWhiteSpace(_profilePath) || !File.Exists(_profilePath))
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = "Сначала выберите профиль (parameters.json).";
                        BtnExportTxt.IsEnabled = false;
                    });
                    return;
                }

                string outPath = null;
                Dispatcher.Invoke(() =>
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = "TXT (CSV, ;) (*.txt)|*.txt|CSV (*.csv)|*.csv|All files (*.*)|*.*",
                        Title = "Сохранить экспорт",
                        FileName = $"RvtExport_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                    };

                    if (dlg.ShowDialog(this) == true)
                        outPath = dlg.FileName;
                });

                if (string.IsNullOrWhiteSpace(outPath))
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Экспорт отменён.");
                    return;
                }

                var exportSvc = new CsvTxtExportService();
                var fields = exportSvc.LoadProfileOrThrow(_profilePath);

                var rows = BuildExportRows(includeRooms: true);
                if (rows.Count == 0)
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Не найдено элементов для экспорта (ни моделей, ни помещений).");
                    return;
                }

                exportSvc.Export(outPath, fields, rows);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = $"Экспорт TXT завершён: {rows.Count} строк.\nФайл: {outPath}";
                });

                await Task.CompletedTask;
            });
        }

        // 5) Экспорт в PostgreSQL (v2) + ProgressBar
        private async void BtnExportPg_Click(object sender, RoutedEventArgs e)
        {
            await RunBusy(async () =>
            {
                if (_currentDb == null)
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Сначала выберите подключение к DB.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_profilePath) || !File.Exists(_profilePath))
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Сначала выберите профиль (parameters.json).");
                    return;
                }

                // Профиль
                var fields = PostgresExportService.LoadProfileOrThrow(_profilePath);

                // Собираем данные Revit (в UI потоке Revit)
                var rows = BuildExportRows(includeRooms: true);
                if (rows.Count == 0)
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = "Не найдено элементов для экспорта (ни моделей, ни помещений).");
                    return;
                }

                // ConnectionString строим из _currentDb
                string connectionString = BuildConnectionStringOrThrow(_currentDb);

                // Подготовим UI прогресс
                Dispatcher.Invoke(() =>
                {
                    Progress.Value = 0;
                    TxtProgress.Text = "Экспорт в PostgreSQL: старт...";
                });

                var svc = new PostgresExportService();
                var opts = new PostgresExportService.ExportOptions
                {
                    BatchSize = 500,
                    CreatedBy = Environment.UserName,
                    SourceModel = _uiDoc.Document?.Title,
                    RevitVersion = "2023",
                    ProfileName = Path.GetFileName(_profilePath),
                    Comment = "MVP v2 export"
                };

                var progress = new Progress<PostgresExportService.ExportProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        double pct = p.TotalRows == 0 ? 100.0 : (100.0 * p.DoneRows / p.TotalRows);
                        if (pct < 0) pct = 0;
                        if (pct > 100) pct = 100;

                        Progress.Value = pct;
                        TxtProgress.Text =
                            $"Export {p.DoneRows}/{p.TotalRows} | batch {p.BatchNumber}/{p.BatchCount} | affected {p.Affected}\n" +
                            $"VersionId: {p.ExportVersionId}";
                    });
                });

                // ВНИМАНИЕ: вызов синхронный (DB внутри). Чтобы UI не зависал — уводим в Task.Run.
                Guid versionId = await Task.Run(() =>
                    svc.ExportVersionedToExportsRawV2(connectionString, fields, rows, opts, progress)
                ).ConfigureAwait(false);

                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = $"Экспорт в PostgreSQL завершён: {rows.Count} строк.\nVersionId: {versionId}";
                    Progress.Value = 100;
                    TxtProgress.Text = $"Готово. VersionId: {versionId}";
                });
            });
        }

        private List<RevitExportCollector.ExportRow> BuildExportRows(bool includeRooms)
        {
            var collector = new RevitExportCollector();
            return collector.CollectFromLinksWithRooms(
                _uiDoc.Document,
                navisViewNameContains: "navisworks",
                includeRooms: includeRooms
            );
        }

        private string GetDefaultConnectionsPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RvtPostgresExporter");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "connections.json");
        }

        /// <summary>
        /// Строит строку подключения Npgsql из DbConnectionConfig.
        /// ВАЖНО: проверь имя свойства пароля в DbConnectionConfig.
        /// Обычно после resolver.Resolve(...) пароль уже доступен в конфиге.
        /// </summary>
        private static string BuildConnectionStringOrThrow(DbConnectionConfig db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            // ⚠️ ВОТ ЭТО МЕСТО МОЖЕТ ПОТРЕБОВАТЬ 1 ПРАВКУ:
            // если пароль хранится не в db.Password, а в другом свойстве — замени здесь.
            string password = db.Password; // <-- если у тебя другое поле, скажи какое (или пришли DbConnectionConfig.cs)

            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Пароль пустой. Проверь secrets/provider в connections.json и resolver.");

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = db.Host,
                Port = db.Port,
                Database = db.Database,
                Username = db.Username,
                Password = password,
                SslMode = SslMode.Disable,
                Timeout = 15,
                CommandTimeout = 120,
                ApplicationName = "RVT-Postgres-Exporter"
            };

            return csb.ConnectionString;
        }

        private void SetUiEnabled(bool enabled)
        {
            BtnLoadProfile.IsEnabled = enabled;
            BtnSelectDb.IsEnabled = enabled;
            BtnCheckDb.IsEnabled = enabled;

            BtnExportTxt.IsEnabled = enabled && !string.IsNullOrWhiteSpace(_profilePath);
            BtnExportPg.IsEnabled = enabled && !string.IsNullOrWhiteSpace(_profilePath) && _currentDb != null;
        }

        private async Task RunBusy(Func<Task> action)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    SetUiEnabled(false);
                    Progress.Visibility = Visibility.Visible;
                    Progress.Value = 0;
                    TxtProgress.Text = "";
                });

                await action();
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = "Ошибка: " + ex.Message;
                    TxtProgress.Text = "";
                });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    Progress.Visibility = Visibility.Collapsed;
                    SetUiEnabled(true);
                });
            }
        }
    }
}