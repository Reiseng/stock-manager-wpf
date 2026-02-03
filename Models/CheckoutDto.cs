using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StockControl.Enums;

namespace StockControl.Models{
public class CheckoutDto
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int ID { get; set; }
    public ClientDto? Client { get; set; }
    public ObservableCollection<CheckoutItemDto> Items { get; set; } = new();
    public DateTime Date { get; set; }
    private InvoiceType _invoiceType;
    public InvoiceType InvoiceType
        {
            get => _invoiceType;
            set
            {
                _invoiceType = value;
                OnPropertyChanged();
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public decimal SubTotal => Items.Sum(i => i.Total);
        public decimal Total => Items.Sum(i => i.Total);
    public static CheckoutDto FromModel(Checkout checkout)
    {
        return new CheckoutDto
        {
            ID = checkout.ID,
            Client = checkout.Client != null
                ? ClientDto.FromModel(checkout.Client)
                : null,
            Date = checkout.Date,
            _invoiceType = checkout.invoiceType,
            Items = new ObservableCollection<CheckoutItemDto>(
                checkout.Items.Select(i => new CheckoutItemDto
                {
                    ProductId = i.product.Id,
                    ProductName = i.product.Name,
                    Unit = i.product.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                })
            )
        };
    }
}
}