using Microsoft.AspNetCore.Mvc;
using Optiq.Embedded.Extentions;
using Optiq.Embedded.Models;
using Optiq.Web.Models;
using Optiq.Web.Services;
using System.Text.Json;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<IProductService,  ProductService>();

        AddOptiqSearch(builder);

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.MapGet("products/index", ([FromServices] IProductService _productService) =>
        {
            _productService.IndexBulk();
        });

        app.MapPost("products", (Product product, [FromServices] IProductService _productService) =>
        {
            _productService.AddProduct(product);
        });

        app.MapPut("products", (Product product, [FromServices] IProductService _productService) =>
        {
            _productService.UpdateProduct(product.Id, product);
        });

        app.MapDelete("products/{id}", (string id, [FromServices] IProductService _productService) =>
        {
            _productService.RemoveProduct(id);
        });

        app.MapGet("products/query", ([AsParameters] ProductQuery query, [FromServices] IProductService _productService) =>
        {
            var filters = string.IsNullOrEmpty(query.filters) ? new OptiqQuery() : JsonSerializer.Deserialize<OptiqQuery>(query.filters);
            return _productService.SearchProducts(query.keyword, filters, query.pageNumber, query.pageSize);
        });

        app.Run();
    }

    private static void AddOptiqSearch(WebApplicationBuilder builder)
    {
        builder.Services.AddOptiq(options =>
        {
            options.UseDb("./optiq.db");
            options.Index<Product>(x =>
            {
                x.Searchable(p => p.Name);
                x.Searchable(p => p.Description);
                x.Filterable(p => p.Brand);
                x.Filterable(p => p.Category);
                x.Filterable(p => p.IsStockAvailable);
                x.Sortable(p => p.Price);
            });
        });
    }

    sealed record ProductQuery(string? keyword, string? filters, int pageNumber, int pageSize);
}
