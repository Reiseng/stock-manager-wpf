using StockControl.Services;
using StockControl.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using StockControl.Dtos;
using QuestPDF.Fluent;

namespace StockControl.ViewModels.Checkouts
{
    public class CheckoutDetailViewModel : INotifyPropertyChange
    {
        private readonly CheckoutService _checkoutService;
        private readonly CheckoutDto _checkoutDto;
        public Company? company;
        public Action<bool>? CloseAction;
        public string ClientFullName =>
            ClientDto != null
                ? $"{ClientDto.Name} {ClientDto.LastName}"
                : "Consumidor final";
        public string ClientDocument =>
            ClientDto?.Dni?? "";
        public event PropertyChangedEventHandler? PropertyChanged;
        public RelayCommand PrintPDFCommand { get; }
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CheckoutDetailViewModel(CheckoutService CheckoutService, CheckoutDto Checkout)
        {
            var companyService = AppServices.CompanyService;
            company = companyService.GetCompanyInfo();
            _checkoutService = CheckoutService;
            _checkoutDto = Checkout;
            ID = Checkout.ID;
            ClientDto = Checkout.Client;
            Items = Checkout.Items;
            Date = Checkout.Date;
            SubTotal = Checkout.SubTotal;
            Total = Checkout.SubTotal*(1+(company.tax/100));
            PrintPDFCommand = new RelayCommand(_ => GenerateInvoicePDF());
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
        private decimal _subtotal;
        public decimal SubTotal
        {
            get => _subtotal;
            set {_subtotal = value;}
        }
        private void GenerateInvoicePDF()
        {
            InvoiceDto invoice = new InvoiceDto(
                ID.ToString(),
                Date,
                _checkoutDto.InvoiceType,
                company!,
                ClientDto!,
                Items.ToList(),
                SubTotal,
                company?.tax ?? 0,
                Total
            );
            var invoiceService = new InvoiceDocumentService(invoice);
            invoiceService.GeneratePdfAndShow();
            CloseAction?.Invoke(false);
        }
    }
    public interface INotifyPropertyChange
    {
    }
}
