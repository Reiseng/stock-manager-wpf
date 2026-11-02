using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Velopack;

namespace StockControl;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must be the first thing: lets the installer/updater run its hooks
        // (install, update, uninstall) and exit before any window is created.
        VelopackApp.Build().Run();

        // Argentine formats everywhere: "$ 1.500,50", "02/11/2026", decimal comma
        var culture = new CultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        // Lets the user type "12," while entering "12,5" in fields that update as you type
        FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
