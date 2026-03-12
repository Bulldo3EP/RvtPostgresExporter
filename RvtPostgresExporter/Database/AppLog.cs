using System;
using System.Diagnostics;
using System.IO;

namespace RvtPostgresExporter.Database
{
    public static class AppLog
    {
        private static readonly object _sync = new object();

        // %APPDATA%\RvtPostgresExporter\logs\plugin.log
        private static string LogFilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "RvtPostgresExporter",
                    "logs");

                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "plugin.log");
            }
        }

        public static void Info(string message) => Write("INFO", message, null);
        public static void Warn(string message) => Write("WARN", message, null);
        public static void Error(string message) => Write("ERROR", message, null);
        public static void Error(string message, Exception ex) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception ex)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{ts}] [{level}] {message}";
            if (ex != null)
                line += Environment.NewLine + ex;

            // Debug Output (видно в Output window VS)
            Debug.WriteLine(line);

            // Файл
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // намеренно глотаем ошибки логирования, чтобы не ломать плагин
            }
        }
    }
}
