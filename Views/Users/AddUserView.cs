using System.Windows;
using System.Windows.Controls;
using StockControl.ViewModels.Users;

namespace StockControl.Views.Users
{
    public partial class AddUserView : UserControl
    {
        public AddUserView()
        {
            InitializeComponent();
        }
        // PasswordBox.Password can't be data-bound, so it is pushed to the ViewModel here
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddUserViewModel vm)
                vm.Password = ((PasswordBox)sender).Password;
        }
    }
}
