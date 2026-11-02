using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StockControl.Enums;
using StockControl.Models;
namespace StockControl.Dtos{
public class CheckoutDto : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public int ID { get; set; }
    public ClientDto? Client { get; set; }
    public ObservableCollection<CheckoutItemDto> Items { get; set; } = new();
    public DateTime Date { get; set; }
    private OperationType _operationType;
    public OperationType OperationType
        {
            get => _operationType;
            set
            {
                _operationType = value;
                OnPropertyChanged();
            }
        }
    public int? RelatedCheckoutId { get; set; }
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

        // Same rounding as ARCA: per VAT rate, 2 decimals
        public decimal Total => Services.Arca.FiscalRules.Amounts(Items.Select(i => (i.Quantity, i.UnitPrice, i.Tax)), '-').Total;
    public static CheckoutDto FromModel(Checkout checkout)
    {
        return new CheckoutDto
        {
            ID = checkout.ID,
            Client = checkout.Client != null
                ? ClientDto.FromModel(checkout.Client)
                : null,
            Date = checkout.Date,
            InvoiceType = checkout.invoiceType,
            OperationType = checkout.operationType,
            Items = new ObservableCollection<CheckoutItemDto>(
                checkout.Items.Select(i => new CheckoutItemDto
                {
                    ProductId = i.product.Id,
                    ProductName = i.product.Name,
                    Unit = i.product.Unit,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Tax = i.product.Tax
                })
            ),
            RelatedCheckoutId = checkout.RelatedCheckoutId
        };
    }
}
}