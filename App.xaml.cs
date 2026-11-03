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
            AppServices.FiscalService =
                new Services.Arca.FiscalService(AppServices.CheckoutPersistence, AppServices.CompanyService);
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

        // Created before any window: they show the installed version (no network here)
        AppServices.UpdateService = new UpdateService();

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
        _ = OfferUpdateIfAvailableAsync();

        // Sales that were left waiting for a CAE (no internet): try again now,
        // every minute while there are any, and as soon as the network comes back
        StartPendingInvoiceRetries();
    }

    private static int _retrying; // 1 while a retry round is running
    private System.Windows.Threading.DispatcherTimer? _retryTimer;

    private void StartPendingInvoiceRetries()
    {
        _ = RetryPendingInvoicesAsync(TimeSpan.FromSeconds(5));

        _retryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _retryTimer.Tick += (_, _) => _ = RetryPendingInvoicesAsync(TimeSpan.Zero);
        _retryTimer.Start();

        System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += (_, args) =>
        {
            if (args.IsAvailable)
                _ = RetryPendingInvoicesAsync(TimeSpan.FromSeconds(5)); // give the connection a moment
        };
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

    private static async Task RetryPendingInvoicesAsync(TimeSpan delay)
    {
        if (Interlocked.Exchange(ref _retrying, 1) == 1)
            return; // a round is already running
        try
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            var fiscal = AppServices.FiscalService;
            if (fiscal == null || !fiscal.IsEnabled || !fiscal.HasPending())
                return;
            var sent = await fiscal.RetryPendingAsync();
            if (sent > 0)
                ErrorLog.Note($"ARCA: se autorizaron {sent} comprobantes pendientes");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Reintentar comprobantes pendientes", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _retrying, 0);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.UpdateService?.ApplyPendingOnExit();
        base.OnExit(e);
    }
}
