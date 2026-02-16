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
using StockControl.Models;
using StockControl.Enums;

namespace StockControl.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public RoleMode ViewMode { get; }
        public User CurrentUser{ get; }
        public string Title { get; private set; }
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
        public ICommand LoggoutCommand { get; }
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
            CurrentUser = AppServices.UserService.CurrentUser;
            Title = $"Usuario: {CurrentUser.Username}";
            if (CurrentUser.Role == RoleType.Admin){
                ViewMode = RoleMode.AdminOnly;
            }
            if (CurrentUser.Role == RoleType.Cashier){
                ViewMode = RoleMode.CashierOnly;
            }
            if (CurrentUser.Role == RoleType.Repository){
                ViewMode = RoleMode.RepositoryOnly;
            }
            if (CurrentView == null)
            {
                CurrentView = new MainView
                {
                    DataContext = this
                };
            }
            LoggoutCommand = new RelayCommand(_ =>
            {
            if (MessageBox.Show(
                                $"¿Seguro de cerrar sesión?",
                                "Confirmar",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                                return;
                AppServices.UserService.Loggout();

                // Abrir login
                var loginVm = new LogginWindowViewModel();
                var loginView = new LogginWindow
                {
                    DataContext = loginVm
                };

                loginView.Show();

                // Cerrar ventana actual
                Application.Current.MainWindow.Close();

                // Setear login como principal
                Application.Current.MainWindow = loginView;
            });
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
                CurrentView = new MainView
                {
                    DataContext = this
                };  
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
