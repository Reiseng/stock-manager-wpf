using System.Windows;
using StockControl.Enums;
using StockControl.Models;
using StockControl.ViewModels;
using StockControl.ViewModels.Checkouts;
using StockControl.Views.Checkouts;

namespace StockControl.Views
{
    // Opens the right home window for a user. Shared by the login form and the
    // automatic login on startup, so both always behave the same way.
    public static class WindowNavigator
    {
        public static void ShowHomeFor(User user)
        {
            Window view = user.Role == RoleType.Cashier
                ? new CheckoutWindow { DataContext = new CheckoutViewModel() }
                : new MainWindow { DataContext = new MainWindowViewModel() };

            var previous = Application.Current.MainWindow;
            view.Show();
            Application.Current.MainWindow = view;
            if (previous != null && previous != view)
                previous.Close();
        }
    }
}
