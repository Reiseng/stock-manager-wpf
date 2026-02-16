using System.Windows;
using System.Windows.Controls;
using StockControl.ViewModels;

namespace StockControl.Views
{
    public partial class LogginWindow : Window
    {
        public LogginWindow()
        {
            InitializeComponent();
        }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LogginWindowViewModel vm)
            {
                vm.Password = ((PasswordBox)sender).Password;
            }
        }
    }
}