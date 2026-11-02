using System.Diagnostics;
using System.Windows;
using StockControl.Services;

namespace StockControl.Views.Windows
{
    public partial class SupportWindow : Window
    {
        // Used from any screen: Command="{x:Static windows:SupportWindow.OpenCommand}"
        public static RelayCommand OpenCommand { get; } = new(_ =>
        {
            var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current.MainWindow;
            var window = new SupportWindow();
            if (owner != null && owner.IsVisible && owner != window)
                window.Owner = owner;
            window.ShowDialog();
        });

        public SupportWindow()
        {
            InitializeComponent();
        }

        private static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception) { MessageBox.Show("No se pudo abrir. Copiá los datos y escribí desde el celular."); }
        }

        private void WhatsApp_Click(object sender, RoutedEventArgs e) => Open(SupportInfo.WhatsAppLink);
        private void Email_Click(object sender, RoutedEventArgs e) => Open(SupportInfo.EmailLink);

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText($"{SupportInfo.Name}\nWhatsApp: {SupportInfo.WhatsAppDisplay}\nEmail: {SupportInfo.Email}");
            ((System.Windows.Controls.Button)sender).Content = "Copiado";
        }
    }
}
