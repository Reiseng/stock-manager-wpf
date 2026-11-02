using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        // ---- Selling without the mouse ----

        // "+" / "-" typed on the EMPTY search box: one more / one less of the last product.
        // Checked on the typed character, so it works with any keyboard layout and the numpad.
        private void SearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb || tb.Text.Length > 0 || DataContext is not CheckoutViewModel vm)
                return;
            if (e.Text == "+")
            {
                vm.ChangeLastQuantity(1);
                e.Handled = true;
            }
            else if (e.Text == "-")
            {
                vm.ChangeLastQuantity(-1);
                e.Handled = true;
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not CheckoutViewModel vm)
                return;

            // Arrow down: go into the list of matching products
            if (e.Key == Key.Down && vm.HasSearchResults)
            {
                FocusResult(0);
                e.Handled = true;
            }
            // Esc: clear the search
            else if (e.Key == Key.Escape)
            {
                SearchBox.Clear();
                vm.ClearSearchResultsCommand.Execute(null);
                vm.SearchMessage = null;
                e.Handled = true;
            }
        }

        // Inside the results list: arrows move (Cycle), Enter adds, Esc goes back
        private void Results_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && DataContext is CheckoutViewModel vm)
            {
                vm.ClearSearchResultsCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ResultsPopup_Closed(object? sender, EventArgs e)
        {
            FocusSearchBox();
        }

        private void FocusResult(int index)
        {
            ResultsList.UpdateLayout();
            if (ResultsList.ItemContainerGenerator.ContainerFromIndex(index) is DependencyObject container)
            {
                var button = FindChild<Button>(container);
                button?.Focus();
            }
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var deeper = FindChild<T>(child);
                if (deeper != null)
                    return deeper;
            }
            return null;
        }

        // Keep the last added/changed product visible in long sales
        private void ItemsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem != null)
                grid.ScrollIntoView(grid.SelectedItem);
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
