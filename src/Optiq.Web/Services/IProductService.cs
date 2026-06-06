using Optiq.Embedded.Models;
using Optiq.Web.Models;

namespace Optiq.Web.Services
{
    public interface IProductService
    {
        void AddProduct(Product product);
        void IndexBulk();
        void RemoveProduct(string id);
        OptiqQueryResult<Product> SearchProducts(string? keyword, OptiqQuery query, int pageNumber, int pageSize);
        void UpdateProduct(string id, Product product);
    }
}