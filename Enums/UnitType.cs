using System.ComponentModel;

namespace StockControl.Enums{
public enum UnitType
{
    [Description("Unidad")]
    Unit,
    [Description("Kilogramos")]
    Kg,
    [Description("Litros")]
    Liter,
    [Description("Caja")]
    Box

}
}