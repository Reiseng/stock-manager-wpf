using System.ComponentModel;

namespace StockControl.Enums{
public enum UnitType
{
    [Description("Unidad")]
    Unit = 0,
    [Description("Kilogramos")]
    Kg = 1,
    [Description("Litros")]
    Liter = 2,
    [Description("Caja")]
    Box = 3
}
}