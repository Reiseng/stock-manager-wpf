using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StockControl.Models;
using StockControl.Services;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Views.Users;
using StockControl.Views.Windows;
using StockControl.Dtos;


namespace StockControl.ViewModels.Users
{
    public class UserViewModel : INotifyPropertyChanged
    {
        private readonly UserService _UserService;
        private readonly ICollectionView _UsersView;
        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();

                EditCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<User> Users { get; }
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _UsersView.Refresh();
            }
        }

        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AddCommand { get; }

        public UserViewModel(UserService UserService)
        {
            _UserService = UserService;
            Users = new ObservableCollection<User>(_UserService.GetUsers(false));

            _UsersView = CollectionViewSource.GetDefaultView(Users);
            _UsersView.Filter = FilterUsers;
            LoadUsers();
            AddCommand = new RelayCommand(_ => OpenAddUser());

            EditCommand = new RelayCommand(
                _ => EditUser(SelectedUser),
                _ => SelectedUser != null);

            DeleteCommand = new RelayCommand(
                _ => DeleteUser(SelectedUser),
                _ => SelectedUser != null);
        }
        private void LoadUsers()
        {
            Users.Clear();

            foreach (var User in _UserService.GetUsers(false))
                Users.Add(User);
        }
        private bool FilterUsers(object obj)
        {
            if (obj is not User User)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return User.Username.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void EditUser(User _User)
        {
            UserDto userDto = UserDto.FromModel(_User);
            var vm = new AddUserViewModel(_UserService, userDto);
            var view = new AddUserWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = success =>
            {
                if (success)
                {
                    new SuccessWindow
                    {
                        Owner = view
                    }.ShowDialog();
                }

                view.Close();
            };

            view.ShowDialog();

            LoadUsers();
            _UsersView.Refresh();
        }
        private void DeleteUser(object parameter)
        {
            try{
                if (parameter is not User User)
                    return;
                if (MessageBox.Show(
                    $"¿Eliminar {User.Username}?",
                    "Confirmar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
                _UserService.Remove(UserDto.FromModel(User));
                Users.Remove(User);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private void OpenAddUser()
        {
            var vm = new AddUserViewModel(_UserService);
            var view = new AddUserWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = success =>
            {
                if (success)
                {
                    new SuccessWindow
                    {
                        Owner = view
                    }.ShowDialog();
                }
                view.Close();
            };
            view.ShowDialog();

            LoadUsers();

            _UsersView.Refresh();
        }
    }
}
