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
        private bool _rememberMe;
        public bool RememberMe
        {
            get => _rememberMe;
            set
            {
                _rememberMe = value;
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
                // Each session starts with an empty cart (it used to carry over between users)
                AppServices.CheckoutService.ClearCheckout();
                try
                {
                    if (RememberMe)
                        AppServices.UserService.RememberCurrentUser();
                    else
                        AppServices.UserService.ForgetSavedSession();
                }
                catch (Exception)
                {
                    // Not being able to save the session must never block the login
                }
                WindowNavigator.ShowHomeFor(user);
            }
            catch(Exception ex)
            {
                 ErrorMessage = ex.Message;
            }
        }
    }
}