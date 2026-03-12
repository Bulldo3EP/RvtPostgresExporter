using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using RvtPostgresExporter.Database;

namespace RvtPostgresExporter.Database.Secrets
{
    public sealed class WindowsCredentialManagerPasswordProvider : IPasswordProvider
    {
        public string GetPassword(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("Credential ref пустой.");

            AppLog.Info("Чтение пароля из Windows Credential Manager: target=" + reference);

            IntPtr credPtr;
            bool ok = CredRead(reference, CRED_TYPE.GENERIC, 0, out credPtr);
            if (!ok)
            {
                var ex = new Win32Exception(Marshal.GetLastWin32Error(), "CredRead failed for: " + reference);
                AppLog.Error("CredRead failed.", ex);
                throw ex;
            }

            try
            {
                // ✅ FIX: PtrToStructure может вернуть null (для nullable-анализатора)
                object credObj = Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
                if (credObj == null)
                {
                    AppLog.Warn("CredRead вернул указатель, но структура не прочиталась (null).");
                    return "";
                }

                var cred = (CREDENTIAL)credObj;

                if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
                {
                    AppLog.Warn("CredentialBlob пустой.");
                    return "";
                }

                var blob = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);

                // Generic credential чаще всего хранится как UTF-16LE строка
                var pwd = Encoding.Unicode.GetString(blob).TrimEnd('\0');

                // fallback на UTF-8 (на всякий случай)
                if (string.IsNullOrEmpty(pwd))
                    pwd = Encoding.UTF8.GetString(blob).TrimEnd('\0');

                AppLog.Info("Пароль успешно прочитан из Credential Manager (значение не логируем).");
                return pwd;
            }
            finally
            {
                CredFree(credPtr);
            }
        }

        private enum CRED_TYPE : uint
        {
            GENERIC = 1
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public uint Flags;
            public CRED_TYPE Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern void CredFree([In] IntPtr cred);
    }
}
