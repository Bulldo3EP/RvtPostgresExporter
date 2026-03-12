namespace RvtPostgresExporter.Profile
{
    public sealed class DbConnectionItem
    {
        public string Name { get; set; }
        public string Database { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }

        public string Display => $"{Name} ({Host}:{Port}/{Database})";
    }
}
