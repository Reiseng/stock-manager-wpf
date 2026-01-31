using StockControl.Config.DataBaseConfig;
using StockControl.Data;
using StockControl.Services;
public static class AppServices
{
    public static DatabaseContext DatabaseContext { get; set; }
    public static ProductPersistence ProductPersistence { get; set; }
    public static ProductService ProductService { get; set; }

    public static ClientPersistence ClientPersistence { get; set; }
    public static ClientService ClientService { get; set; }
    
    public static CheckoutPersistence CheckoutPersistence { get; set; }
    public static CheckoutService CheckoutService { get; set; }
}
