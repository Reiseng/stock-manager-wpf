using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StockControl.Services;
using System.Windows;
using StockControl.Models;

namespace StockControl.ViewModels.Clients
{
    public class AddClientViewModel : INotifyPropertyChanged
    {

        private readonly ClientService _ClientService;
        private readonly ClientDto clientDto;
        public bool IsEditMode { get; private set; }
        public string Title => IsEditMode ? "Editar cliente" : "Agregar cliente";
        public ClientDto? CreatedClient { get; private set; }
        public Action? ShowSuccessAction { get; set; }
        public Action<bool>? CloseAction;
        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set { _isSuccess = value; OnPropertyChanged(); }
        }

        public AddClientViewModel(ClientService ClientService)
        {
            _ClientService = ClientService;
            clientDto = new ClientDto();
            IsEditMode = false;
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }
        // EDITAR
        public AddClientViewModel(ClientService ClientService, ClientDto Client)
            : this(ClientService)
        {
            IsEditMode = true;
            clientDto = Client;
            Name = Client.Name;
            LastName = Client.LastName;
            Dni = Client.Dni;
            Address = Client.Address;
            Phone = Client.Phone;
            Email = Client.Email;
        }
        private string _dni;
        public string Dni
        {
            get => _dni;
            set { _dni = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private string _lastname;
        public string LastName
        {
            get => _lastname;
            set { _lastname = value; OnPropertyChanged(); 
                SaveCommand.RaiseCanExecuteChanged();}
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; 
                    OnPropertyChanged(); 
                    SaveCommand.RaiseCanExecuteChanged();
                }
        }

        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        private string _phone;
        public string Phone
        {
            get => _phone;
            set { _phone = value; 
                    OnPropertyChanged(); 
                }
        }
        private string _address;
        public string Address
        {
            get => _address;
            set { _address = value; 
                    OnPropertyChanged(); 
                }
        }

        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

private void Save()
{
    try{
        clientDto.Name = Name;
        clientDto.LastName = LastName;
        clientDto.Dni = Dni;
        clientDto.Phone = Phone;
        clientDto.Address = Address;
        clientDto.Email = Email;
        if (IsEditMode)
        {
            _ClientService.UpdateClient(clientDto);
        }
        else
        {
            _ClientService.AddClient(clientDto);
            CreatedClient = ClientDto.FromModel(_ClientService.GetClientByDni(clientDto.Dni));
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
            return !string.IsNullOrWhiteSpace(Name)
                   && !string.IsNullOrWhiteSpace(Dni)
                   && !string.IsNullOrWhiteSpace(LastName);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
