using System.Windows;
using System.Windows.Controls;

namespace StockControl.Views.Clients
{
    public partial class ClientView : UserControl
    {
        public ClientView()
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
