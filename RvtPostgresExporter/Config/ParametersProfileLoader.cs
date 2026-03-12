using System;
using System.IO;
using Newtonsoft.Json;

namespace RvtPostgresExporter.Config
{
    public static class ParametersProfileLoader
    {
        public static ExportProfile LoadOrThrow(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("path пустой");

            if (!File.Exists(path))
                throw new FileNotFoundException("parameters.json не найден: " + path);

            var json = File.ReadAllText(path);
            var profile = JsonConvert.DeserializeObject<ExportProfile>(json);

            if (profile == null)
                throw new InvalidOperationException("Не удалось распарсить parameters.json");

            if (profile.Columns == null || profile.Columns.Count == 0)
                throw new InvalidOperationException("В parameters.json отсутствуют columns");

            return profile;
        }

        public static string GetDefaultProfilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RvtPostgresExporter"
            );
            return Path.Combine(dir, "parameters.json");
        }
    }
}