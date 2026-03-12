using System.Collections.Generic;
using Newtonsoft.Json;

namespace RvtPostgresExporter.Database
{
    public sealed class ConnectionsConfig
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("defaults")]
        public ConnectionDefaults Defaults { get; set; } = new ConnectionDefaults();

        [JsonProperty("connections")]
        public List<ConnectionEntry> Connections { get; set; } = new List<ConnectionEntry>();
    }

    public sealed class ConnectionDefaults
    {
        [JsonProperty("host")]
        public string Host { get; set; } = "localhost";

        [JsonProperty("port")]
        public int Port { get; set; } = 5432;

        [JsonProperty("username")]
        public string Username { get; set; } = "postgres";

        [JsonProperty("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 15;

        [JsonProperty("commandTimeoutSeconds")]
        public int CommandTimeoutSeconds { get; set; } = 30;

        [JsonProperty("sslMode")]
        public string SslMode { get; set; } = "Disable";

        [JsonProperty("applicationName")]
        public string ApplicationName { get; set; } = "RVT-Postgres-Exporter";
    }

    public sealed class ConnectionEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int? Port { get; set; }

        [JsonProperty("database")]
        public string Database { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("timeoutSeconds")]
        public int? TimeoutSeconds { get; set; }

        [JsonProperty("commandTimeoutSeconds")]
        public int? CommandTimeoutSeconds { get; set; }

        [JsonProperty("sslMode")]
        public string SslMode { get; set; }

        [JsonProperty("applicationName")]
        public string ApplicationName { get; set; }

        [JsonProperty("password")]
        public PasswordRef Password { get; set; }
    }

    public sealed class PasswordRef
    {
        [JsonProperty("provider")]
        public string Provider { get; set; } // WindowsCredentialManager | DpapiFile | EnvironmentVariable

        [JsonProperty("ref")]
        public string Ref { get; set; }
    }
}
