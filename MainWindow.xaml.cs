using System.Windows;

namespace StockControl.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // El DataContext ya lo pusimos en XAML, así que ni siquiera hace falta ponerlo aquí.
            // DataContext = new MainWindowViewModel();
        }

    }
}