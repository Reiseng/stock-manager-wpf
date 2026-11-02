using StockControl.Enums;
using StockControl.Models;
using StockControl.Services.Arca;

namespace StockControl.Dtos
{
    // Everything the PDF and the "operation registered" dialog need
    public class InvoiceDto
    {
        public string invoiceNumber { get; set; }
        public DateTime date { get; set; }
        public InvoiceType invoiceType {get; set; }
        public OperationType operationType { get; set; }
        public Company seller { get; set; }
        public ClientDto client { get; set; }
        public List<CheckoutItemDto> items { get; set; }
        public decimal total { get; set; }

        // ---- ARCA ----
        public FiscalStatus FiscalStatus { get; set; } = FiscalStatus.Internal;
        public int VoucherType { get; set; }
        public int PointOfSale { get; set; }
        public long VoucherNumber { get; set; }
        public string? Cae { get; set; }
        public DateTime? CaeDueDate { get; set; }
        public string? FiscalMessage { get; set; }
        public bool Production { get; set; }
        public int DocType { get; set; } = 99;
        public long DocNumber { get; set; }
        public VoucherAmounts? Amounts { get; set; }

        public bool IsFiscal => FiscalStatus == FiscalStatus.Authorized;
        public char Letter => VoucherType switch { 1 or 2 or 3 => 'A', 6 or 7 or 8 => 'B', 11 or 12 or 13 => 'C', _ => ' ' };
        public string VoucherTitle => IsFiscal ? FiscalRules.VoucherTypeName(VoucherType) : OperationTitle;
        public string FormattedNumber => IsFiscal ? FiscalRules.FormatNumber(PointOfSale, VoucherNumber) : invoiceNumber;
        private string OperationTitle => operationType switch
        {
            OperationType.Budget => "Presupuesto",
            OperationType.CreditNote => "Nota de crédito (interna)",
            OperationType.DebitNote => "Nota de débito (interna)",
            _ => "Comprobante interno"
        };

        public InvoiceDto(string InvoiceNumber, DateTime Date,InvoiceType InvoiceType, OperationType OperationType, Company Seller, ClientDto Client, List<CheckoutItemDto> Items, decimal Total)
        {
            invoiceNumber = InvoiceNumber;
            date = Date;
            invoiceType = InvoiceType;
            operationType = OperationType;
            seller = Seller;
            client = Client;
            items = Items;
            total = Total;
        }

        public static InvoiceDto From(Checkout checkout, Company company)
        {
            var items = checkout.Items.Select(i => new CheckoutItemDto
            {
                ProductId = i.product.Id,
                ProductName = i.product.Name,
                Unit = i.product.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Tax = i.product.Tax
            }).ToList();

            var client = checkout.Client != null ? ClientDto.FromModel(checkout.Client) : new ClientDto { Name = "Consumidor", LastName = "Final" };
            var dto = new InvoiceDto(checkout.ID.ToString(), checkout.Date, checkout.invoiceType, checkout.operationType,
                company, client, items, checkout.Total)
            {
                FiscalStatus = checkout.FiscalStatus,
                VoucherType = checkout.FiscalVoucherType ?? 0,
                PointOfSale = checkout.FiscalPointOfSale ?? 0,
                VoucherNumber = checkout.FiscalVoucherNumber ?? 0,
                Cae = checkout.Cae,
                CaeDueDate = checkout.CaeDueDate,
                FiscalMessage = checkout.FiscalMessage,
                Production = checkout.FiscalProduction,
            };
            var doc = FiscalRules.Document(checkout.Client);
            dto.DocType = doc.DocType;
            dto.DocNumber = doc.DocNumber;
            if (dto.IsFiscal)
            {
                dto.Amounts = FiscalRules.Amounts(checkout.Items.Select(i => (i.Quantity, i.UnitPrice, i.product.Tax)), dto.Letter);
                dto.total = dto.Amounts.Total;
            }
            return dto;
        }
    }
}
