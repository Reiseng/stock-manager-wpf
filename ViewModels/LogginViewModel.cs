using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SQLitePCL;
using StockControl.Models;
using StockControl.ViewModels.Checkouts;
using StockControl.Views;
using StockControl.Views.Checkouts;

namespace StockControl.ViewModels
{
    public class LogginWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        private string _username { get; set; }
        public string Username
        {
            get => _username;
            set {
                _username = value;
                OnPropertyChanged();
                }
        }
        private string _password { get; set; }
        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }
        private string _errorMessage;

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
        private User user { get; set; }
        public ICommand LogginCommand { get; }

        public LogginWindowViewModel()
        {
            LogginCommand = new RelayCommand (_=> ExecuteLoggin());
        }
        public void ExecuteLoggin()
        {
            try{
                
                user = AppServices.UserService.Login(Username, Password);
                if (user.Role == Enums.RoleType.Admin || user.Role == Enums.RoleType.Repository)
                {
                    var vm = new MainWindowViewModel();
                    var view = new MainWindow
                    {
                        DataContext = vm
                    };

                    view.Show();

                    Application.Current.MainWindow.Close();
                    Application.Current.MainWindow = view;
                }
                if (user.Role == Enums.RoleType.Cashier)
                {
                    var vm = new CheckoutViewModel();
                    var view = new CheckoutWindow
                    {
                        DataContext = vm
                    };

                    view.Show();

                    Application.Current.MainWindow.Close();
                    Application.Current.MainWindow = view;
                }
            }
            catch(Exception ex)
            {
                 ErrorMessage = ex.Message;
            }
        }
    }
}