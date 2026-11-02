using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;
using QuestPDF.Infrastructure;
using StockControl.Config.DataBaseConfig;
using StockControl.Data;
using StockControl.Models;
using StockControl.Services;
using StockControl.Services.Database;
using StockControl.Utils;

namespace StockControl;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        QuestPDF.Settings.License = LicenseType.Community; //QuestPDF License
        base.OnStartup(e);

        // Any unexpected error in a screen: log it and keep the program open
        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLog.Write("Error no controlado", args.Exception);
            MessageBox.Show(
                $"Ocurrió un error inesperado:\n\n{args.Exception.Message}\n\nEl detalle quedó guardado en {ErrorLog.FilePath}",
                "Stock Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };

        try
        {
            ConfigService.Load();
            ThemeService.Initialize(ConfigService.Config.Theme);

            var dbPath = ConfigService.DatabaseFullPath;
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true
            }.ToString();

            AppServices.DatabaseContext = new DatabaseContext(connectionString);
            AppServices.BackupService = new BackupService(AppServices.DatabaseContext, dbPath);
            // Applies pending migrations (with a safety copy first) and base data
            var dbInitializer = new DatabaseInitializer(AppServices.DatabaseContext, AppServices.BackupService);
            dbInitializer.Initialize();
            try
            {
                AppServices.BackupService.DailyBackupIfNeeded();
            }
            catch (Exception)
            {
                // A failed daily copy (full disk, permissions...) must not stop the store
            }

            AppServices.UserPersistence =
                new UserPersistence(AppServices.DatabaseContext);
            AppServices.UserService =
                new UserService(AppServices.UserPersistence, dbPath);
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
        catch (Exception ex)
        {
            ErrorLog.Write("Inicio del programa", ex);
            MessageBox.Show(
                $"No se pudo iniciar el programa:\n\n{ex.Message}",
                "Stock Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // "Mantener sesión iniciada": go straight in if there is a valid saved session
        User? rememberedUser = null;
        try
        {
            rememberedUser = AppServices.UserService.TryAutoLogin();
        }
        catch (Exception)
        {
            // Never block startup because of a saved session: just ask for the password
        }

        if (rememberedUser != null)
        {
            AppServices.CheckoutService.ClearCheckout();
            Views.WindowNavigator.ShowHomeFor(rememberedUser);
        }
        else
        {
            // Opened here instead of StartupUri so it never opens after a failed startup
            new Views.LogginWindow().Show();
        }

        // Checked in the background: the program is usable right away, and
        // without internet nothing happens
        AppServices.UpdateService = new UpdateService();
        _ = OfferUpdateIfAvailableAsync();
    }

    private async Task OfferUpdateIfAvailableAsync()
    {
        try
        {
            var update = await AppServices.UpdateService.CheckAsync();
            if (update == null)
                return;
            ErrorLog.Note($"Actualización disponible: {update.TargetFullRelease.Version}");

            var vm = new ViewModels.UpdatePromptViewModel(AppServices.UpdateService, update);
            var window = new Views.Windows.UpdateWindow { DataContext = vm };
            if (MainWindow != null && MainWindow.IsVisible)
                window.Owner = MainWindow;
            vm.Close = () => window.Close();
            ErrorLog.Note("Mostrando el aviso de actualización");
            window.ShowDialog();
            ErrorLog.Note("Aviso de actualización cerrado");
        }
        catch (Exception ex)
        {
            // An update problem must never get in the way of selling
            ErrorLog.Write("Aviso de actualización", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.UpdateService?.ApplyPendingOnExit();
        base.OnExit(e);
    }
}
