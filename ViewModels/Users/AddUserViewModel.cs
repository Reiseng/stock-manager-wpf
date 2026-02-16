using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Enums;
using StockControl.Dtos;

namespace StockControl.ViewModels.Users
{
    public class AddUserViewModel : INotifyPropertyChanged
    {

        private readonly UserService _userService;
        private readonly UserDto? _User;
        public bool IsEditMode { get; private set; }
        public string Title => IsEditMode ? "Editar usuario" : "Agregar usuario";

        public Action? ShowSuccessAction { get; set; }
        public UserDto? CreatedUser { get; private set; }
        public Action<bool>? CloseAction;
        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public AddUserViewModel(UserService userService)
        {
            IsEditMode = false;
            _userService = userService;
            _User = new UserDto();
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));

            RoleTypes = Enum.GetValues(typeof(RoleType));
        }
        // EDITAR
        public AddUserViewModel(UserService userService, UserDto user)
            : this(userService)
        {
            IsEditMode = true;
            _User = user;
            Username = user.Username;
            Password = "";
            SelectedRoleType = user.Role.Value;
        }
        private string _username;
        public string Username
        {
            get => _username;
            set { _username = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); }
        }

        private RoleType _selectedRoleType;
        public RoleType SelectedRoleType
        {
            get => _selectedRoleType;
            set { _selectedRoleType = value; OnPropertyChanged(); }
        }

        public Array RoleTypes { get; }

        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

private void Save()
{
    try{
        _User.Username = Username;
        _User.Password = Password;
        _User.Role = SelectedRoleType;
        if (IsEditMode)
        {
            _userService.Update(_User);
        }
        else
        {
            _userService.AddUser(_User);
        }

        ShowSuccessAction?.Invoke();
                    Task.Delay(600).ContinueWith(_ =>
                        Application.Current.Dispatcher.Invoke(() =>
                            CloseAction?.Invoke(true)));
        }
    catch(Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
}

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Username);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
