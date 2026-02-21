using System.IO;
using System.Text.Json;

public static class ConfigService
{
    public static AppConfig Config { get; private set; }

    public static void Load()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        if (!File.Exists(path))
        {
            var defaultConfig = new AppConfig
            {
                DatabasePath = "Databases/app.db"
            };

            var tjson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, tjson);
        }

        var json = File.ReadAllText(path);

        Config = JsonSerializer.Deserialize<AppConfig>(json);
    }
}
public class AppConfig
{
    public string DatabasePath { get; set; }
}