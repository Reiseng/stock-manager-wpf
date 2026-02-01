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
        public Client? SelectedClient
        {
            get => _selectedClient;
            set
            {
                _selectedClient = value;
                OnPropertyChanged();

                EditCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
                ConfirmCommand.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<Client> Clients { get; }
        public ClientViewMode ViewMode { get; }
        public Action<Client?>? CloseAction { get; set; }
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
        public RelayCommand ConfirmCommand { get; }

        public ClientsViewModel(ClientService ClientService, bool canEditClients)
        {
            if (canEditClients)
                ViewMode = ClientViewMode.Editable;
            else
                ViewMode = ClientViewMode.SelectOnly;
            _ClientService = ClientService;
            Clients = new ObservableCollection<Client>(_ClientService.GetClients(false));

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
                
            ConfirmCommand = new RelayCommand(
                _ => CloseAction?.Invoke(SelectedClient),
                _ => SelectedClient != null);
        }
        private void LoadClients()
        {
            Clients.Clear();

            foreach (var Client in _ClientService.GetClients(false))
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

            LoadClients();
            _ClientsView.Refresh();
        }
        private void DeleteClient(object parameter)
        {
            try{
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

        private void OpenAddClient()
        {
            ClientDto? createdClient = null;

            var vm = new AddClientViewModel(_ClientService);
            var view = new AddClientWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };

            vm.CloseAction = success =>
            {
                if (success)
                {
                    createdClient = vm.CreatedClient;
                    new SuccessWindow
                    {
                        Owner = view
                    }.ShowDialog();
                }

                view.Close();
            };

            view.ShowDialog();

            if (createdClient != null)
            {
                LoadClients();

                SelectedClient = Clients.FirstOrDefault(c => c.ID == createdClient.ID);
             if (ViewMode == ClientViewMode.SelectOnly && SelectedClient != null)
                {
                    CloseAction?.Invoke(SelectedClient);
                }
            }

            _ClientsView.Refresh();
        }
    }
}
