using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;
using StockControl.Config.DataBaseConfig;

public static class ConfigService
{
    private const string DefaultDatabasePath = "Databases/app.db";

    public static AppConfig Config { get; private set; } = new AppConfig { DatabasePath = DefaultDatabasePath };

    // All the data (database, backups, config, saved session) lives OUTSIDE the
    // program folder. The installer replaces the program folder on every update,
    // so anything stored next to the .exe would be lost.
    // %LocalAppData%\StockManagerData\
    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StockManagerData");

    private static string ConfigPath => Path.Combine(DataFolder, "config.json");

    // Business logo printed on the vouchers (loaded from Configuración)
    public static string LogoPath => Path.Combine(DataFolder, "logo.png");

    // Absolute path of the database. A relative path in config.json is resolved
    // against the data folder (not the exe folder or the working directory).
    public static string DatabaseFullPath
    {
        get
        {
            var path = string.IsNullOrWhiteSpace(Config.DatabasePath) ? DefaultDatabasePath : Config.DatabasePath;
            return Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(Path.Combine(DataFolder, path));
        }
    }

    // First time the program runs on this PC (no configuration and no database yet):
    // the database setup assistant is shown.
    public static bool IsNewInstall { get; private set; }

    public static void Load()
    {
        Directory.CreateDirectory(DataFolder);

        if (!File.Exists(ConfigPath))
        {
            MoveLegacyData();
            IsNewInstall = !File.Exists(DatabaseFullPath);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        try
        {
            Config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? Config;
        }
        catch (JsonException ex)
        {
            throw new Exception($"El archivo de configuración está dañado ({ConfigPath}): {ex.Message}");
        }
    }

    public static void SaveTheme(string themeId)
    {
        Config.Theme = themeId;
        Save();
    }

    // ---- Database connection ----
    public static DatabaseProvider Provider => Config.Database.Provider;

    public static DatabaseContext CreateDatabaseContext() => CreateDatabaseContext(Config.Database);

    public static DatabaseContext CreateDatabaseContext(DatabaseSettings settings)
    {
        if (settings.Provider == DatabaseProvider.PostgreSql)
            return new DatabaseContext(PostgresConnectionString(settings), DatabaseProvider.PostgreSql);

        Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFullPath)!);
        return new DatabaseContext(SqliteConnectionString(DatabaseFullPath), DatabaseProvider.Sqlite);
    }

    public static string SqliteConnectionString(string path)
        => new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();

    public static string PostgresConnectionString(DatabaseSettings s, string? database = null)
        => new NpgsqlConnectionStringBuilder
        {
            Host = s.Host,
            Port = s.Port,
            Database = database ?? s.Name,
            Username = s.Username,
            Password = s.GetPassword(),
            Timeout = 8,            // seconds to connect: a dead server must not freeze the register
            CommandTimeout = 60,
        }.ToString();

    // Identifies the database (shown in Configuración; saved sessions belong to one database)
    public static string DatabaseDescription => Provider == DatabaseProvider.PostgreSql
        ? $"PostgreSQL {Config.Database.Username}@{Config.Database.Host}:{Config.Database.Port}/{Config.Database.Name}"
        : DatabaseFullPath;

    public static void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception)
        {
            // Not being able to save a preference is not a reason to fail
        }
    }

    // Older versions kept the database next to the .exe (Databases\app.db).
    // On the first run of this version that database is copied to the data folder.
    private static void MoveLegacyData()
    {
        var legacyDb = Path.Combine(AppContext.BaseDirectory, "Databases", "app.db");
        var legacyConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
        try
        {
            if (File.Exists(legacyConfig))
            {
                var old = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(legacyConfig));
                if (!string.IsNullOrWhiteSpace(old?.DatabasePath))
                    legacyDb = Path.IsPathRooted(old.DatabasePath)
                        ? old.DatabasePath
                        : Path.Combine(AppContext.BaseDirectory, old.DatabasePath);
            }

            if (File.Exists(legacyDb) && !File.Exists(DatabaseFullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFullPath)!);
                File.Copy(legacyDb, DatabaseFullPath);
            }
        }
        catch (Exception)
        {
            // Nothing to migrate or not readable: start with a new database
        }
    }
}
public class AppConfig
{
    public string? DatabasePath { get; set; }
    // Color theme chosen in Configuración (see ThemeService). Saved per PC.
    public string? Theme { get; set; }
    // Electronic invoicing. Per PC: each register can have its own point of sale.
    public ArcaConfig Arca { get; set; } = new();
    // Where the data lives: a SQLite file on this PC or a PostgreSQL server
    public DatabaseSettings Database { get; set; } = new();
    // Ticket printer. Per PC: each register has its own printer.
    public TicketConfig Ticket { get; set; } = new();
}

public class DatabaseSettings
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    // PostgreSQL server
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = "stockmanager";
    public string Username { get; set; } = "postgres";
    // Encrypted with Windows (DPAPI): config.json never has the password in plain text
    public string? ProtectedPassword { get; set; }

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("StockManager.Database.v1");

    public string GetPassword()
    {
        if (string.IsNullOrEmpty(ProtectedPassword))
            return "";
        try
        {
            var data = Convert.FromBase64String(ProtectedPassword);
            return Encoding.UTF8.GetString(OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser)
                : data);
        }
        catch (Exception)
        {
            return ""; // from another Windows user or PC: it has to be typed again
        }
    }

    public void SetPassword(string password)
    {
        var data = Encoding.UTF8.GetBytes(password ?? "");
        ProtectedPassword = Convert.ToBase64String(OperatingSystem.IsWindows()
            ? ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser)
            : data);
    }

    public DatabaseSettings Copy() => (DatabaseSettings)MemberwiseClone();
}

public class TicketConfig
{
    public string? Printer { get; set; }
    public int PaperWidthMm { get; set; } = 80;   // 58 or 80
    // Print the ticket as soon as a sale is confirmed, without asking
    public bool AutoPrint { get; set; }
}

public class ArcaConfig
{
    public bool Enabled { get; set; }
    // false = homologación (ARCA test environment, nothing is real)
    public bool Production { get; set; }
    public int PointOfSale { get; set; } = 1;
    // Whether "Facturar en ARCA" comes checked in new sales
    public bool InvoiceByDefault { get; set; }
}
