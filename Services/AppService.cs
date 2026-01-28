using StockControl.Data;
using StockControl.Services;
public static class AppServices
{
    public static ProductPersistence ProductPersistence { get; } = new ProductPersistence();
    public static ProductService ProductService { get; } =
        new ProductService(ProductPersistence);

    public static ClientPersistence ClientPersistence { get; } = new ClientPersistence();
    public static ClientService ClientService { get; } =
        new ClientService(ClientPersistence);
    
    public static CheckoutPersistence CheckoutPersistence { get; } = new CheckoutPersistence();
    public static CheckoutService CheckoutService { get; } =
        new CheckoutService(CheckoutPersistence);
}
