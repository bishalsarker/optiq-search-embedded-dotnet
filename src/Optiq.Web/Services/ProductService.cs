using Optiq.Embedded.Interfaces;
using Optiq.Embedded.Models;
using Optiq.Web.Models;

namespace Optiq.Web.Services
{
    public class ProductService : IProductService
    {
        private readonly IOptiqContext _context;

        public ProductService(IOptiqContext context)
        {
            _context = context;
        }

        public void IndexBulk()
        {
            var dummyProducts = new List<Product>
            {
                new()
                {
                    Id = "prod_001",
                    Name = "XPS 15 Premium Laptop",
                    Description = "High performance developer laptop with aluminum chassis and ultra-sharp display.",
                    Brand = "Dell",
                    Category = "Computers",
                    Price = 1499.99m,
                    IsStockAvailable = true,
                },
                new()
                {
                    Id = "prod_002",
                    Name = "Inspiron Home Desktop",
                    Description = "Affordable family desktop computer for daily productivity, tasks, and media browsing.",
                    Brand = "Dell",
                    Category = "Desktops",
                    Price = 549.00m,
                    IsStockAvailable = true,
                },
                new()
                {
                    Id = "prod_003",
                    Name = "MacBook Pro M3",
                    Description = "Next-generation professional laptop with cutting-edge silicone chip and extreme battery life.",
                    Brand = "Apple",
                    Category = "Computers",
                    Price = 1999.50m,
                    IsStockAvailable = false,
                },
                new()
                {
                    Id = "prod_004",
                    Name = "Mechanical Wireless Keyboard",
                    Description = "Tactile typing experience with backlit clicky mechanical switches and Bluetooth connectivity.",
                    Brand = "Logitech",
                    Category = "Accessories",
                    Price = 129.99m,
                    IsStockAvailable = false,
                },
                new()
                {
                    Id = "prod_005",
                    Name = "MX Master 3S Ergonomic Mouse",
                    Description = "High-precision wireless mouse with silent clicks and hyper-fast scroll wheel.",
                    Brand = "Logitech",
                    Category = "Accessories",
                    Price = 99.00m,
                    IsStockAvailable = false,
                },
                new()
                {
                    Id = "prod_006",
                    Name = "ProDesk Business Mini PC",
                    Description = "Compact form-factor enterprise desktop computer with legacy ports and robust security.",
                    Brand = "HP",
                    Category = "Desktops",
                    Price = 649.99m,
                    IsStockAvailable = false,
                },
                new()
                {
                    Id = "prod_007",
                    Name = "Ergonomic Mesh Office Chair",
                    Description = "Premium high-back desk chair with lumbar support and multi-dimensional armrests.",
                    Brand = "Herman Miller",
                    Category = "Furniture",
                    Price = 899.00m,
                    IsStockAvailable = true,
                }
            };

            // Seeding implementation lookup loop
            IOptiqSet<Product> productSet = _context.SetEntity<Product>();
            foreach (var product in dummyProducts)
            {
                productSet.Upsert(product.Id, product);
            }

            // Category & Form Factor Synonyms
            _context.AddSynonymGroup("laptop", "notebook", "portable", "macbook", "xps");
            _context.AddSynonymGroup("desktop", "pc", "tower", "workstation", "mini pc", "computer");
            _context.AddSynonymGroup("accessories", "peripherals", "hardware", "input devices");
            _context.AddSynonymGroup("furniture", "office gear", "office equipment");

            // Component & Technology Synonyms
            _context.AddSynonymGroup("display", "screen", "monitor", "panel", "visuals");
            _context.AddSynonymGroup("silicon", "chip", "processor", "cpu", "m3");
            _context.AddSynonymGroup("chassis", "frame", "body", "build", "case");
            _context.AddSynonymGroup("wireless", "bluetooth", "cordless");

            // Specific Product Type Synonyms
            _context.AddSynonymGroup("keyboard", "keys", "board", "mechanical switches");
            _context.AddSynonymGroup("mouse", "mice", "pointer", "trackpad");
            _context.AddSynonymGroup("chair", "seat", "seating", "throne", "desk chair");

            // Attribute & Intent Synonyms
            _context.AddSynonymGroup("premium", "high-end", "pro", "professional", "top-tier");
            _context.AddSynonymGroup("affordable", "budget", "cheap", "value", "family");
            _context.AddSynonymGroup("ergonomic", "comfortable", "health", "lumbar support");
            _context.AddSynonymGroup("productivity", "work", "tasks", "daily use", "business");
        }

        public void AddProduct(Product product)
        {
            var productCollection = _context.SetEntity<Product>();
            productCollection.Upsert(product.Id, product);
        }

        public void UpdateProduct(string id, Product product)
        {
            var productCollection = _context.SetEntity<Product>();
            productCollection.Upsert(id, product);
        }

        public void RemoveProduct(string id)
        {
            var productCollection = _context.SetEntity<Product>();
            productCollection.Remove(id);
        }

        public OptiqQueryResult<Product> SearchProducts(string? keyword, OptiqQuery query, int pageNumber, int pageSize)
        {
            var productCollection = _context.SetEntity<Product>();
            return productCollection.Search(keyword, query, (pageNumber - 1) * pageSize, pageSize);
        }
    }
}
