using System.ComponentModel;

namespace StockControl.Enums{
public enum RoleType
{
    [Description("Administrador")]
    FinalConsumer = 0,

    [Description("Cajero")]
    AFacture = 1,

    [Description("Repositor")]
    BFacture = 2
}
}