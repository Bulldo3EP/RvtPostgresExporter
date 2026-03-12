using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RvtPostgresExporter.Database;
using RvtPostgresExporter.Profile;

namespace RvtPostgresExporter.UI
{
    public partial class ConnectionSelectorWindow : Window
    {
        public string SelectedConnectionName { get; private set; }

        public ConnectionSelectorWindow()
        {
            InitializeComponent();
        }

        public void LoadFromConnectionsConfig(ConnectionsConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var d = cfg.Defaults ?? new ConnectionDefaults();

            var items = new List<DbConnectionItem>();
            foreach (var c in cfg.Connections)
            {
                items.Add(new DbConnectionItem
                {
                    Name = c.Name,
                    Database = c.Database,
                    Host = string.IsNullOrWhiteSpace(c.Host) ? d.Host : c.Host,
                    Port = c.Port ?? d.Port
                });
            }

            ConnectionsList.ItemsSource = items.OrderBy(x => x.Name).ToList();
        }

        private void ConnectionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionsList.SelectedItem is DbConnectionItem item)
            {
                SelectedConnectionName = item.Name;
                BtnOk.IsEnabled = true;
            }
            else
            {
                SelectedConnectionName = null;
                BtnOk.IsEnabled = false;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SelectedConnectionName))
            {
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
