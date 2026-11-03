using System.Windows;
using StockControl.ViewModels.Settings;

namespace StockControl.Views.Windows
{
    public partial class DatabaseSetupWindow : Window
    {
        private readonly DatabaseSetupViewModel _vm;

        public DatabaseSetupWindow(bool firstRun)
        {
            InitializeComponent();
            _vm = new DatabaseSetupViewModel(firstRun);
            DataContext = _vm;
            PasswordInput.Password = _vm.Password; // PasswordBox can't be bound
            _vm.Close = saved => DialogResult = saved; // closes the window
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
            => _vm.Password = PasswordInput.Password;
    }
}
