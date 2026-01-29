using System.ComponentModel;

namespace StockControl.Enums{
public enum InvoiceType
{
    [Description("Consumidor Final")]
    FinalConsumer,
    [Description("Factura A")]
    AFacture,
    [Description("Factura B")]
    BFacture,
    [Description("Factura C")]
    CFacture
}
}