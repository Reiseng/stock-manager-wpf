using System.Windows;
using System.Windows.Controls;

namespace StockControl.Views.Users
{
    public partial class UsersView : UserControl
    {
        public UsersView()
        {
            InitializeComponent();
        }
        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.Focus();
        }
    }
}