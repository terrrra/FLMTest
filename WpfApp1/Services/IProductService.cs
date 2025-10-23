using FLMDesktop.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Services
{
    public interface IProductService
    {
        Task<List<Product>> GetAllAsync(CancellationToken ct = default);
        Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<Product> CreateAsync(Product product, CancellationToken ct = default);
        Task UpdateAsync(Product product, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}