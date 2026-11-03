using System.IO;
using System.Text.Json;

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

    public static void Load()
    {
        Directory.CreateDirectory(DataFolder);

        if (!File.Exists(ConfigPath))
        {
            MoveLegacyData();
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
    // Ticket printer. Per PC: each register has its own printer.
    public TicketConfig Ticket { get; set; } = new();
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
