using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StockControl.ViewModels.Checkouts;

namespace StockControl.Views.Checkouts
{
    public partial class CheckoutWindow : Window
    {
        public CheckoutWindow()
        {
            InitializeComponent();
        }
        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                tb.Focus();
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                if (DataContext is CheckoutViewModel vm)
                {
                    vm.AddProductFromSearch();
                    tb.Clear();
                    tb.Focus();
                }
            }
        }
    }
}
