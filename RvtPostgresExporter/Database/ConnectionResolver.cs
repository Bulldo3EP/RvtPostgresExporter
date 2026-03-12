using System;
using System.Linq;
using RvtPostgresExporter.Database.Secrets;

namespace RvtPostgresExporter.Database
{
    public sealed class ConnectionResolver
    {
        public DbConnectionConfig Resolve(ConnectionsConfig cfg, string connectionName)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(connectionName)) throw new ArgumentException("connectionName пустой.");

            var entry = cfg.Connections.FirstOrDefault(c =>
                c.Name != null && c.Name.Equals(connectionName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
                throw new InvalidOperationException("Не найдено подключение в connections.json: " + connectionName);

            var d = cfg.Defaults ?? new ConnectionDefaults();

            var host = string.IsNullOrWhiteSpace(entry.Host) ? d.Host : entry.Host;
            var port = entry.Port ?? d.Port;
            var username = string.IsNullOrWhiteSpace(entry.Username) ? d.Username : entry.Username;
            var timeout = entry.TimeoutSeconds ?? d.TimeoutSeconds;
            var cmdTimeout = entry.CommandTimeoutSeconds ?? d.CommandTimeoutSeconds;
            var ssl = string.IsNullOrWhiteSpace(entry.SslMode) ? d.SslMode : entry.SslMode;
            var appName = string.IsNullOrWhiteSpace(entry.ApplicationName) ? d.ApplicationName : entry.ApplicationName;

            if (entry.Password == null)
                throw new InvalidOperationException("В подключении отсутствует password: " + entry.Name);

            var provider = PasswordProviderFactory.Create(entry.Password.Provider);
            var password = provider.GetPassword(entry.Password.Ref);

            return new DbConnectionConfig
            {
                Name = entry.Name,
                Host = host,
                Port = port,
                Database = entry.Database,
                Username = username,
                Password = password,
                TimeoutSeconds = timeout,
                CommandTimeoutSeconds = cmdTimeout,
                SslMode = ssl,
                ApplicationName = appName
            };
        }
    }
}
