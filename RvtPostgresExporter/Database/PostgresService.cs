using System;
using System.Threading.Tasks;
using Npgsql;

namespace RvtPostgresExporter.Database
{
    /// <summary>
    /// Лёгкий сервис для работы с PostgreSQL (пока используем только проверку подключения).
    /// Проверка выполняется в отдельном AppDomain через DbAppDomainHost, чтобы избежать конфликтов сборок в Revit.
    /// </summary>
    public sealed class PostgresService
    {
        private readonly DbConnectionConfig _cfg;

        public PostgresService(DbConnectionConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        /// <summary>Собирает строку подключения из DbConnectionConfig.</summary>
        public string GetConnectionString()
        {
            // NpgsqlConnectionStringBuilder корректно экранирует значения.
            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = _cfg.Host,
                Port = _cfg.Port,
                Database = _cfg.Database,
                Username = _cfg.Username,
                Password = _cfg.Password,
                Timeout = _cfg.TimeoutSeconds,
                CommandTimeout = _cfg.CommandTimeoutSeconds,
                ApplicationName = string.IsNullOrWhiteSpace(_cfg.ApplicationName) ? "RVT-Postgres-Exporter" : _cfg.ApplicationName
            };

            // SslMode хранится строкой в json: Disable/Require/Prefer/Allow/VerifyCA/VerifyFull
            if (!string.IsNullOrWhiteSpace(_cfg.SslMode) &&
                Enum.TryParse(_cfg.SslMode, ignoreCase: true, out SslMode ssl))
            {
                csb.SslMode = ssl;
            }

            return csb.ConnectionString;
        }

        public Task<bool> CheckConnectionAsync()
        {
            // Не блокируем UI поток.
            return Task.Run(() =>
            {
                var cs = GetConnectionString();
                string err;
                var ok = DbAppDomainHost.CheckConnectionIsolated(cs, out err);

                if (!ok && !string.IsNullOrWhiteSpace(err))
                    AppLog.Error("Postgres connection check failed: " + err);

                return ok;
            });
        }
    }
}
