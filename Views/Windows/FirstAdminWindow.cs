using System.Windows;
using StockControl.Dtos;
using StockControl.Enums;
using StockControl.Models;
using StockControl.Services;
using StockControl.Utils;

namespace StockControl.Views.Windows
{
    // First run on a brand-new database: there is no default account anymore,
    // the person installing chooses the administrator's user name and password.
    public partial class FirstAdminWindow : Window
    {
        public User? CreatedUser { get; private set; }

        public FirstAdminWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => PasswordInput.Focus();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameInput.Text.Trim();
            var password = PasswordInput.Password;
            try
            {
                if (username.Length == 0)
                    throw new Exception("Escribí un nombre de usuario.");
                if (password.Length < 8)
                    throw new Exception("La contraseña tiene que tener al menos 8 caracteres.");
                if (password != RepeatInput.Password)
                    throw new Exception("Las dos contraseñas no coinciden.");

                // Another register may have created it meanwhile (shared server)
                if (AppServices.UserService.GetUsers(true).Count == 0)
                    AppServices.UserService.AddUser(new UserDto(username, password, RoleType.Admin, true));
                CreatedUser = AppServices.UserService.Login(username, password);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Crear administrador", ex);
                ErrorText.Text = ex.Message;
                ErrorText.Visibility = Visibility.Visible;
            }
        }
    }
}
