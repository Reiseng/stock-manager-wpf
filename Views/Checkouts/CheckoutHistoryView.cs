using System.Windows;
using System.Windows.Controls;

namespace StockControl.Views.Checkouts
{
    public partial class CheckoutHistoryView : UserControl
    {
        public CheckoutHistoryView()
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
