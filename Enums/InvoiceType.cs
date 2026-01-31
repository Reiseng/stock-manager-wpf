using System.ComponentModel;

namespace StockControl.Enums{
public enum InvoiceType
{
    [Description("Consumidor Final")]
    FinalConsumer = 0,

    [Description("Factura A")]
    AFacture = 1,

    [Description("Factura B")]
    BFacture = 2,

    [Description("Factura C")]
    CFacture = 3
}
}