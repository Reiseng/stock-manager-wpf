using System.ComponentModel;

namespace StockControl.Enums{
public enum RoleType
{
    [Description("Administrador")]
    Admin = 0,

    [Description("Cajero")]
    Cashier = 1,

    [Description("Repositor")]
    Repository = 2
}
}