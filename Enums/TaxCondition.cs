using System.ComponentModel;

namespace StockControl.Enums
{
    public enum TaxCondition
    {
        [Description("Responsable Inscripto")]
        ResponsibleRegistered = 0,
        [Description("Monotributista")]
        Monotax = 1,
        [Description("Exento")]
        Exempt = 2,
        [Description("Consumidor Final")]
        FinalConsumer = 3,
        [Description("No Responsable")]
        NotResponsible = 4
    }
}