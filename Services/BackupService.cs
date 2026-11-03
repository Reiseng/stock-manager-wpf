using System.IO;
using Microsoft.Data.Sqlite;
using StockControl.Config.DataBaseConfig;
using StockControl.Data.Ef;
using StockControl.Services.Database;

namespace StockControl.Services
{
    // Export / import of the whole database and automatic backups.
    //
    // The copies are always SQLite files (.db), whatever the engine:
    //  - SQLite: SQLite's online backup API (BackupDatabase) instead of copying the
    //    file: a consistent copy even while the program is using the database.
    //  - PostgreSQL: every row is copied to a new .db file (DataTransfer). The same
    //    file can be imported back into a server, or into a PC without a server.
    public class BackupService
    {
        private const int AutomaticBackupsToKeep = 30;
        private const string AutomaticPrefix = "auto-";
        private const string DailyReason = "diaria";

        private readonly DatabaseContext db;
        private readonly string databasePath;

        public BackupService(DatabaseContext dbContext, string databasePath)
        {
            db = dbContext;
            this.databasePath = databasePath;
        }

        public string DatabasePath => databasePath;
        public string BackupFolder => db.IsSqlite
            ? Path.Combine(Path.GetDirectoryName(databasePath)!, "Backups")
            : Path.Combine(ConfigService.DataFolder, "Backups-servidor");

        public static string SuggestedExportFileName()
            => $"StockManager-copia-{DateTime.Now:yyyyMMdd-HHmm}.db";

        public DateTime? LastAutomaticBackup()
        {
            if (!Directory.Exists(BackupFolder))
                return null;
            var last = Directory.GetFiles(BackupFolder, AutomaticPrefix + "*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            return last?.LastWriteTime;
        }

        // One automatic copy per day, made on the first startup of the day
        public void DailyBackupIfNeeded()
        {
            // Only daily copies count: a copy made before importing/migrating has the OLD data
            var todayPattern = $"{AutomaticPrefix}{DateTime.Now:yyyyMMdd}-*-{DailyReason}.db";
            if (Directory.Exists(BackupFolder) &&
                Directory.GetFiles(BackupFolder, todayPattern).Length > 0)
                return;
            CreateAutomaticBackup(DailyReason);
        }

        public string CreateAutomaticBackup(string reason)
        {
            Directory.CreateDirectory(BackupFolder);
            var path = Path.Combine(BackupFolder, $"{AutomaticPrefix}{DateTime.Now:yyyyMMdd-HHmmss}-{reason}.db");
            ExportTo(path);
            PruneAutomaticBackups();
            return path;
        }

        public void ExportTo(string destinationPath)
        {
            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            if (!db.IsSqlite)
            {
                ExportServerTo(destinationPath);
                return;
            }

            using var source = db.CreateConnection();
            source.Open();
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Pooling = false // release the file as soon as we finish
            }.ToString());
            destination.Open();
            source.BackupDatabase(destination);
        }

        // Server -> new .db file with the same schema and all the rows
        private void ExportServerTo(string destinationPath)
        {
            var file = new DatabaseContext(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                ForeignKeys = true,
                Pooling = false
            }.ToString());
            try
            {
                new DatabaseInitializer(file).Initialize();
                DataTransfer.CopyAll(db, file);
            }
            catch
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(destinationPath); } catch (IOException) { }
                throw;
            }
            SqliteConnection.ClearAllPools();
        }

        // .db file -> server. The file is brought up to date on a temporary copy
        // first; the server is replaced in one transaction (all or nothing).
        private void ImportIntoServer(string sourcePath)
        {
            var temp = Path.Combine(Path.GetTempPath(), $"stockmanager-importar-{Guid.NewGuid():N}.db");
            File.Copy(sourcePath, temp);
            try
            {
                var file = new DatabaseContext(new SqliteConnectionStringBuilder
                {
                    DataSource = temp,
                    ForeignKeys = true,
                    Pooling = false
                }.ToString());
                new DatabaseInitializer(file).Initialize();
                DataTransfer.CopyAll(file, db);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(temp); } catch (IOException) { }
            }
        }

        // Replaces the current data with the content of the given file.
        // A safety copy of the current data is made first.
        public void ImportFrom(string sourcePath)
        {
            if (!db.IsSqlite)
            {
                ValidateBackupFile(sourcePath);
                CreateAutomaticBackup("antes-de-importar");
                ImportIntoServer(sourcePath);
                return;
            }

            if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(databasePath), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Ese archivo es la base de datos que se está usando ahora.");

            ValidateBackupFile(sourcePath);
            var safetyCopy = CreateAutomaticBackup("antes-de-importar");

            try
            {
                CopyInto(sourcePath);
                // An older backup gets the missing migrations applied
                new DatabaseInitializer(db, this).Initialize();
            }
            catch
            {
                // Something failed after replacing the data: put the previous data back
                CopyInto(safetyCopy);
                throw;
            }
        }

        private void CopyInto(string sourcePath)
        {
            using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString()))
            using (var destination = db.CreateConnection())
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination);
            }
            SqliteConnection.ClearAllPools();
        }

        // Checks that the file is a healthy StockControl database that this
        // version of the program can open, BEFORE touching the current data.
        public static void ValidateBackupFile(string path)
        {
            if (!File.Exists(path))
                throw new Exception("El archivo no existe.");

            try
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());
                connection.Open();

                using (var check = connection.CreateCommand())
                {
                    check.CommandText = "PRAGMA quick_check;";
                    var result = check.ExecuteScalar() as string;
                    if (result != "ok")
                        throw new Exception("El archivo está dañado.");
                }

                using (var tables = connection.CreateCommand())
                {
                    tables.CommandText = @"
                        SELECT COUNT(*) FROM sqlite_master
                        WHERE type = 'table' AND name IN ('Users', 'Products', 'Clients', 'Checkouts', 'CheckoutItems');";
                    if (Convert.ToInt64(tables.ExecuteScalar()) < 5)
                        throw new Exception("El archivo no es una copia de este programa.");
                }

                var version = DatabaseInitializer.GetSchemaVersion(connection);
                if (version > DatabaseInitializer.LatestVersion)
                    throw new Exception("La copia fue hecha con una versión más nueva del programa. Actualizá el programa primero.");
            }
            catch (SqliteException)
            {
                throw new Exception("El archivo no es una base de datos válida.");
            }
        }

        private void PruneAutomaticBackups()
        {
            var old = Directory.GetFiles(BackupFolder, AutomaticPrefix + "*.db")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Skip(AutomaticBackupsToKeep);
            foreach (var file in old)
            {
                try { file.Delete(); } catch (IOException) { /* in use: try next time */ }
            }
        }
    }
}
