using System.ComponentModel;

namespace StockControl.Enums
{
    // What happened with a sale in ARCA
    public enum FiscalStatus
    {
        [Description("Interno")]
        Internal = 0,     // only recorded in the program, not sent to ARCA
        [Description("Pendiente")]
        Pending = 1,      // must get a CAE; not obtained yet (no internet, ARCA down...)
        [Description("Con CAE")]
        Authorized = 2,   // ARCA approved it
        [Description("Rechazado")]
        Rejected = 3      // ARCA rejected it (see FiscalMessage)
    }
}
