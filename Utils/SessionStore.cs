using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StockControl.Utils
{
    public record SavedSession(int UserId, string Token, string DatabasePath);

    // Keeps the "keep me signed in" token on this PC, encrypted with Windows DPAPI
    // (DataProtectionScope.CurrentUser): only the same Windows user on the same PC
    // can read it. If the file is copied to another PC it is useless.
    public static class SessionStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StockManager.Session.v1");

        private static string FilePath => Path.Combine(ConfigService.DataFolder, "session.dat");

        public static void Save(SavedSession session)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(session);
            var protectedBytes = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(FilePath, protectedBytes);
        }

        // Any problem (no file, other Windows user, corrupt file) => no saved session
        public static SavedSession? Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;
                var json = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<SavedSession>(json);
            }
            catch (Exception)
            {
                Clear();
                return null;
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
