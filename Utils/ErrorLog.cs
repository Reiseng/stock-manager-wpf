using System.IO;

namespace StockControl.Utils
{
    // Errors that are handled silently (update check, unexpected UI errors...)
    // are written to %LocalAppData%\StockManagerData\errores.log so they can be
    // diagnosed later. Never throws.
    public static class ErrorLog
    {
        private const long MaxSize = 1_000_000;

        public static string FilePath => Path.Combine(ConfigService.DataFolder, "errores.log");

        public static void Write(string where, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(ConfigService.DataFolder);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxSize)
                    file.Delete();
                File.AppendAllText(FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{UpdateServiceVersion()} - {where}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception)
            {
            }
        }

        public static void Note(string message)
        {
            try
            {
                Directory.CreateDirectory(ConfigService.DataFolder);
                File.AppendAllText(FilePath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] v{UpdateServiceVersion()} - {message}{Environment.NewLine}");
            }
            catch (Exception)
            {
            }
        }

        private static string UpdateServiceVersion()
            => System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?";
    }
}
