using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using StockControl.Dtos;

public class CompanyComponent : IComponent
{
    private string Title { get; }
    private Company company { get; }

    public CompanyComponent(string title, Company company)
    {
        Title = title;
        this.company = company;
    }

    public void Compose(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(2);

            column.Item().BorderBottom(1).PaddingBottom(5).Text(Title).SemiBold();

            column.Item().Text(company.Name);
            column.Item().Text(company.CUIT);
            column.Item().Text(company.taxCondition.ToString());
            column.Item().Text("IVA: " + company.tax+"%");
            column.Item().Text(company.Address);
            column.Item().Text(company.Email);
            column.Item().Text(company.Phone);
        });
    }
}
public class ClientComponent : IComponent
{
    private string Title { get; }
    private ClientDto client { get; }

    public ClientComponent(string title, ClientDto client)
    {
        Title = title;
        this.client = client;
    }

    public void Compose(IContainer container)
    {
        container.Column(column =>
        {
            column.Spacing(2);

            column.Item().BorderBottom(1).PaddingBottom(5).Text(Title).SemiBold();

            column.Item().Text(client.Dni);
            column.Item().Text(client.Name);
            column.Item().Text(client.LastName);
            column.Item().Text(client.Address);
            column.Item().Text(client.Email);
            column.Item().Text(client.Phone);
        });
    }
}