using System.Collections.Generic;

namespace RvtPostgresExporter.Database
{
    public sealed class ConnectionsLoadResult
    {
        public bool IsValid { get; set; }
        public ConnectionsConfig Config { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }
}
