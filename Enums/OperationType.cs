using System.ComponentModel;
namespace StockControl.Enums{
public enum OperationType
    {
        [Description("Venta")]
        Sale = 0,
        [Description("Presupuesto")]
        Budget = 1,
        [Description("Nota de Crédito")]
        CreditNote = 2,
        [Description("Nota de Débito")]
        DebitNote = 3
    }
}