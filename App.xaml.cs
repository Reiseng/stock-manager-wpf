using System.IO;
using System.Windows;
using QuestPDF.Infrastructure;
using StockControl.Config.DataBaseConfig;
using StockControl.Data;
using StockControl.Services;
using StockControl.Services.Database;

namespace StockControl;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
        {
            QuestPDF.Settings.License = LicenseType.Community; //QuestPDF License
            base.OnStartup(e);

            var dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Databases",
                "app.db"
            );

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            var connectionString = $"Data Source={dbPath}";

            AppServices.DatabaseContext = new DatabaseContext(connectionString);
            var dbInitializer = new DatabaseInitializer(AppServices.DatabaseContext);
            dbInitializer.Initialize();

            AppServices.UserPersistence = 
                new UserPersistence(AppServices.DatabaseContext);
            AppServices.UserService = 
                new UserService(AppServices.UserPersistence);

            AppServices.ProductPersistence =
                new ProductPersistence(AppServices.DatabaseContext);
            AppServices.ProductService =
                new ProductService(AppServices.ProductPersistence);
            AppServices.ClientPersistence =
                new ClientPersistence(AppServices.DatabaseContext);
            AppServices.ClientService =
                new ClientService(AppServices.ClientPersistence);
            AppServices.CompanyPersistence =
                new CompanyPersistence(AppServices.DatabaseContext);
            AppServices.CompanyService =
                new CompanyService(AppServices.CompanyPersistence);
            AppServices.CheckoutPersistence =
                new CheckoutPersistence(AppServices.DatabaseContext, 
                    AppServices.ClientService, 
                    AppServices.ProductService);
            AppServices.CheckoutService =
                new CheckoutService(AppServices.CheckoutPersistence);

            }

}

