using StockControl.Services;
using StockControl.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace StockControl.ViewModels.Checkouts
{
    public class CheckoutDetailViewModel : INotifyPropertyChange
    {
        private readonly CheckoutService _checkoutService;
        private readonly CheckoutDto _checkoutDto;
        public Action? CloseAction { get; set; }
        public string ClientFullName =>
            ClientDto != null
                ? $"{ClientDto.Name} {ClientDto.LastName}"
                : "Consumidor final";
        public string ClientDocument =>
            ClientDto?.Dni?? "";
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CheckoutDetailViewModel(CheckoutService CheckoutService, CheckoutDto Checkout)
        {
            _checkoutService = CheckoutService;
            _checkoutDto = Checkout;
            ID = Checkout.ID;
            ClientDto = Checkout.Client;
            Items = Checkout.Items;
            Date = Checkout.Date;
            Total = Checkout.Total;
        }
        private int _id;
        public int ID
        {
            get => _id;
            set { _id = value;}
        }
        private ClientDto? _clientDto;
        public ClientDto? ClientDto
        {
            get => _clientDto;
            set
            {
                _clientDto = value;
                OnPropertyChanged(nameof(ClientDto));
                OnPropertyChanged(nameof(ClientFullName));
                OnPropertyChanged(nameof(ClientDocument));
            }
        }
        private ObservableCollection<CheckoutItemDto> _items;
        public ObservableCollection<CheckoutItemDto> Items
        {
            get => _items;
            set { _items = value;}
        }
        private DateTime _date;
        public DateTime Date
        {
            get => _date;
            set { _date = value;}
        }
        private decimal _total;
        public decimal Total
        {
            get => _total;
            set {_total = value;}
        }
    }

    public interface INotifyPropertyChange
    {
    }
}
