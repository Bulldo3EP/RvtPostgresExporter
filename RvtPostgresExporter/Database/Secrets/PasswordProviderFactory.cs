using System;

namespace RvtPostgresExporter.Database.Secrets
{
    public static class PasswordProviderFactory
    {
        public static IPasswordProvider Create(string providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("providerName пустой.");

            switch (providerName.Trim())
            {
                case "WindowsCredentialManager":
                    return new WindowsCredentialManagerPasswordProvider();
                case "DpapiFile":
                    return new DpapiFilePasswordProvider();
                case "EnvironmentVariable":
                    return new EnvironmentVariablePasswordProvider();
                default:
                    throw new ArgumentException("Неизвестный password.provider: " + providerName);
            }
        }
    }
}
