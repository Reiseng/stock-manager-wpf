using System.Windows.Controls;

namespace StockControl.Views.Settings
{
    public partial class SettingsPanelView : UserControl
    {
        public SettingsPanelView()
        {
            InitializeComponent();
        }
        // PasswordBox.Password can't be data-bound
        private void PfxPassword_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is StockControl.ViewModels.Settings.SettingsViewModel vm)
                vm.PfxPassword = ((System.Windows.Controls.PasswordBox)sender).Password;
        }
    }
}