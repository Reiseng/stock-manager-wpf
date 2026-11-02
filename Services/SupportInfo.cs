namespace StockControl.Services
{
    // Contact data shown in the "Soporte" window. Change it here (one place).
    public static class SupportInfo
    {
        public const string Name = "Facundo Reiseng";
        public const string Email = "facu_carhue@hotmail.com";
        // Shown as is
        public const string WhatsAppDisplay = "+54 9 2936 40-4816";
        // For the wa.me link: 54 + 9 (mobile) + area code + number, digits only
        public const string WhatsAppNumber = "5492936404816";

        public const string Message =
            "Stock Manager se puede usar gratis.\n" +
            "La instalación, la configuración de la facturación electrónica (ARCA), " +
            "las capacitaciones y la ayuda ante problemas se cobran aparte.";

        public static string WhatsAppLink =>
            $"https://wa.me/{WhatsAppNumber}?text={Uri.EscapeDataString("Hola, te escribo por Stock Manager.")}";

        public static string EmailLink =>
            $"mailto:{Email}?subject={Uri.EscapeDataString("Soporte Stock Manager " + UpdateService.CurrentVersionText)}";
    }
}
