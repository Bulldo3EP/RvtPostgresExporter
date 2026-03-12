using System;

namespace RvtPostgresExporter.Database.Secrets
{
    public sealed class EnvironmentVariablePasswordProvider : IPasswordProvider
    {
        public string GetPassword(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("Environment variable name пустое.");

            var value = Environment.GetEnvironmentVariable(reference);
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException("Не найдена переменная окружения: " + reference);

            return value;
        }
    }
}
