using System.Windows;
using System.Windows.Controls;

namespace StockControl.Views.Products
{
    public partial class ProductView : UserControl
    {
        public ProductView()
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
