using System.ComponentModel;
using System.Windows.Input;
using StockControl.Views.Clients;
using StockControl.Views.Products;
using StockControl.Views.Users;
using StockControl.Views.Settings;
using StockControl.ViewModels.Products;
using StockControl.ViewModels.Clients;
using StockControl.ViewModels.Checkouts;
using StockControl.Views.Checkouts;
using System.Windows;
using StockControl.ViewModels.Settings;
using StockControl.Views;
using StockControl.ViewModels.Users;

namespace StockControl.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
            }
        }
        public ICommand ShowMainViewCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand ShowProductsCommand { get; }
        public ICommand ShowClientsCommand { get; }
        public ICommand ShowUsersCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ShowCheckoutHistoryCommand {get; }
        public ICommand OpenCheckoutCommand =>
            new RelayCommand(_ =>
            {
                var vm = new CheckoutViewModel();
                var view = new CheckoutWindow
                {
                    DataContext = vm,
                    Owner = Application.Current.MainWindow
                };
                view.ShowDialog();
            });
        public MainWindowViewModel()
        {
            if (CurrentView == null)
            {
                CurrentView = new MainView();
            }
            // Every command sets the CurrentView to the appropriate View with its ViewModel
            ShowProductsCommand = new RelayCommand(_ =>
            {
                CurrentView = new ProductView
                {
                    DataContext = new ProductsViewModel(AppServices.ProductService, CanEditOrDelete: true)
                };
            });

            ShowClientsCommand = new RelayCommand(_ =>
            {
                CurrentView = new ClientView
                {
                    DataContext = new ClientsViewModel(AppServices.ClientService, canEditClients: true)
                };
            });
            ShowCheckoutHistoryCommand = new RelayCommand(_ =>
            {
                CurrentView = new CheckoutHistoryView
                {
                    DataContext = new CheckoutsHistoryViewModel(AppServices.CheckoutService)
                };
            });
            ShowUsersCommand = new RelayCommand(_ =>
            {
                // If we need internal navegation on UserView, we can set its DataContext here
                CurrentView = new UsersView{ DataContext = new UserViewModel(AppServices.UserService)};
            });

            ShowSettingsCommand = new RelayCommand(_ =>
            {
                CurrentView = new SettingsPanelView{ DataContext = new SettingsViewModel(AppServices.CompanyService)};
            });
            ShowMainViewCommand = new RelayCommand(_ =>
            {
                CurrentView = new MainView();  
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
