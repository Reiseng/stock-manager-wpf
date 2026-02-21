using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var grid = (DataGrid)sender;

                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    if (DataContext is CheckoutViewModel vm)
                    {
                        vm.LoadCheckout();
                    }
                    FocusSearchBox();
                }), DispatcherPriority.Background);
            }
        }
        private void FocusSearchBox()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }), DispatcherPriority.Background);
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
        private void RelatedCheckoutIdText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb)
            {
                if (DataContext is CheckoutViewModel vm)
                {
                    vm.SetRelatedCheckoutIdFromText();
                    tb.Clear();
                    tb.Focus();
                }
            }
        }
    }
}
