using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using StockControl.Models;
using StockControl.Services;
using System.Runtime.CompilerServices;
using System.Windows;
using StockControl.Views.Clients;
using StockControl.Views.Windows;


namespace StockControl.ViewModels.Clients
{
    public class ClientsViewModel : INotifyPropertyChanged
    {
        private readonly ClientService _ClientService;
        private readonly ICollectionView _ClientsView;
        private Client? _selectedClient;

        public ObservableCollection<Client> Clients { get; }

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                _ClientsView.Refresh();
            }
        }

        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand AddCommand { get; }

        public ClientsViewModel(ClientService ClientService)
        {
            _ClientService = ClientService;

            Clients = new ObservableCollection<Client>(_ClientService.GetClients());

            _ClientsView = CollectionViewSource.GetDefaultView(Clients);
            _ClientsView.Filter = FilterClients;
            LoadClients();
            AddCommand = new RelayCommand(_ => OpenAddClient());

            EditCommand = new RelayCommand(
                _ => EditClient(SelectedClient),
                _ => SelectedClient != null);

            DeleteCommand = new RelayCommand(
                _ => DeleteClient(SelectedClient),
                _ => SelectedClient != null);
        }
        private void LoadClients()
        {
            Clients.Clear();

            foreach (var Client in _ClientService.GetClients())
                Clients.Add(Client);
        }
        private bool FilterClients(object obj)
        {
            if (obj is not Client Client)
                return false;

            if (string.IsNullOrWhiteSpace(SearchText))
                return true;

            return Client.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || Client.Dni.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void EditClient(Client _Client)
        {
            ClientDto _clientdto = new ClientDto(_Client.ID,_Client.Dni, _Client.Name, _Client.LastName, _Client.Email, _Client.Phone, _Client.Address);
            var vm = new AddClientViewModel(_ClientService, _clientdto);
            var view = new AddClientWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.ShowSuccessAction = () =>
            {
                new SuccessWindow
                {
                    Owner = view
                }.Show();
            };
            vm.CloseAction = () => view.Close();

            view.ShowDialog();

            LoadClients();
            _ClientsView.Refresh();
        }
        private void DeleteClient(object parameter)
        {
            if (parameter is not Client Client)
                return;
            if (MessageBox.Show(
                $"¿Eliminar {Client.Name}?",
                "Confirmar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            _ClientService.RemoveClient(Client.ID);
            Clients.Remove(Client);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
        public Client? SelectedClient
        {
            get => _selectedClient;
            set
            {
                _selectedClient = value;
                OnPropertyChanged();

                EditCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
        private void OpenAddClient()
        {
            var vm = new AddClientViewModel(_ClientService);
            var view = new AddClientWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.ShowSuccessAction = () =>
            {
                new SuccessWindow
                {
                    Owner = view
                }.Show();
            };
            vm.CloseAction = () => view.Close();
            view.ShowDialog();

            LoadClients();
            _ClientsView.Refresh();
        }
    }
}
