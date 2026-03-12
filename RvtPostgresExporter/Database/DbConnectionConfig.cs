namespace RvtPostgresExporter.Database
{
    public sealed class DbConnectionConfig
    {
        public string Name { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; } // не логировать!
        public int TimeoutSeconds { get; set; }
        public int CommandTimeoutSeconds { get; set; }
        public string SslMode { get; set; }
        public string ApplicationName { get; set; }
    }
}
