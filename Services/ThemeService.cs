using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace StockControl.Services
{
    public class ThemeOption
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public bool IsDark { get; init; }
        // Colors for the little preview in Configuración
        public Brush PreviewSidebar { get; init; } = Brushes.Black;
        public Brush PreviewBackground { get; init; } = Brushes.White;
        public Brush PreviewPrimary { get; init; } = Brushes.Gray;
        public Brush PreviewAccent { get; init; } = Brushes.Gold;
    }

    // Switches the color palette while the program is running.
    // Each theme is a file in Themes/Palettes with the same keys (Brush.Bg,
    // Brush.Primary...). Screens use them as DynamicResource, so replacing the
    // dictionary repaints everything at once. The choice is saved per PC.
    public static class ThemeService
    {
        public static IReadOnlyList<ThemeOption> Themes { get; } = new[]
        {
            Make("Verde", "Verde", "Claro. El original.", false, "#1F3A33", "#EEF2F0", "#2F6B57", "#E0A526"),
            Make("Oscuro", "Oscuro", "Cansa menos la vista con poca luz.", true, "#0E1412", "#131A18", "#6BB89A", "#E3AC34"),
            Make("Azul", "Azul", "Claro, con azul marino.", false, "#1B2A42", "#EDF0F5", "#2D5C9A", "#E0A526"),
            Make("Bordo", "Bordó", "Claro, con tonos vino.", false, "#3B1D27", "#F3EEEF", "#8A3A50", "#D9A441"),
        };

        public static ThemeOption Current { get; private set; } = Themes[0];

        public static void Initialize(string? savedThemeId)
        {
            // Every window that opens gets the right title bar (dark or light)
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler((sender, _) => ApplyTitleBar((Window)sender)));
            Apply(savedThemeId, save: false);
        }

        public static void Apply(string? themeId, bool save = true)
        {
            var theme = Themes.FirstOrDefault(t => t.Id == themeId) ?? Themes[0];
            var palette = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/StockManager;component/Themes/Palettes/{theme.Id}.xaml", UriKind.Absolute)
            };

            var merged = Application.Current.Resources.MergedDictionaries;
            var old = merged.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Palettes/"));
            if (old != null)
                merged[merged.IndexOf(old)] = palette;
            else
                merged.Insert(0, palette);

            Current = theme;
            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);

            if (save)
                ConfigService.SaveTheme(theme.Id);
        }

        private static ThemeOption Make(string id, string name, string description, bool dark,
            string sidebar, string background, string primary, string accent) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            IsDark = dark,
            PreviewSidebar = Frozen(sidebar),
            PreviewBackground = Frozen(background),
            PreviewPrimary = Frozen(primary),
            PreviewAccent = Frozen(accent),
        };

        private static Brush Frozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        // ---- Windows title bar in dark mode (Windows 10 2004+ / Windows 11) ----
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private static void ApplyTitleBar(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;
                int dark = Current.IsDark ? 1 : 0;
                // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE; 19 on older Windows 10 builds
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
            }
            catch (Exception)
            {
                // Older Windows: the title bar just stays light
            }
        }
    }
}
