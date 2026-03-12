using System;
using System.IO;
using System.Security.Cryptography;   // ✅ ВАЖНО: для ProtectedData / DataProtectionScope
using System.Text;
using RvtPostgresExporter.Database;

namespace RvtPostgresExporter.Database.Secrets
{
    /// <summary>
    /// Пароль хранится в файле в виде DPAPI-encrypted blob (CurrentUser).
    /// Файл может быть:
    ///  - бинарным (raw bytes)
    ///  - или base64-строкой
    /// </summary>
    public sealed class DpapiFilePasswordProvider : IPasswordProvider
    {
        public string GetPassword(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("DpapiFile ref пустой.");

            var path = Environment.ExpandEnvironmentVariables(reference);
            AppLog.Info("Чтение DPAPI secret file: " + path);

            if (!File.Exists(path))
            {
                var msg = "DPAPI secret file не найден: " + path;
                AppLog.Error(msg);
                throw new FileNotFoundException(msg);
            }

            byte[] bytes = File.ReadAllBytes(path);

            // файл может быть base64-текстом или сырыми байтами
            byte[] encrypted;
            try
            {
                var s = Encoding.UTF8.GetString(bytes).Trim();
                encrypted = Convert.FromBase64String(s);
            }
            catch
            {
                encrypted = bytes;
            }

            try
            {
                var clear = ProtectedData.Unprotect(encrypted, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
                var password = Encoding.UTF8.GetString(clear);

                AppLog.Info("DPAPI пароль успешно расшифрован (значение не логируем).");
                return password;
            }
            catch (CryptographicException cex)
            {
                var msg = "Ошибка расшифровки DPAPI secret file. Возможно файл создан другим пользователем Windows или повреждён.";
                AppLog.Error(msg, cex);
                throw;
            }
        }

        /// <summary>
        /// Утилита для создания DPAPI-файла (можно вызвать из отдельной кнопки/утилиты).
        /// </summary>
        public static void CreateEncryptedFile(string filePath, string password)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath пустой.");

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var clear = Encoding.UTF8.GetBytes(password ?? string.Empty);
            var encrypted = ProtectedData.Protect(clear, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);

            File.WriteAllBytes(filePath, encrypted);
            AppLog.Info("DPAPI secret file создан: " + filePath);
        }
    }
}
