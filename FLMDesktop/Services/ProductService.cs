using FLMDesktop.Data;
using FLMDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Services
{
    public class ProductService : IProductService
    {
        private readonly string _conn;
        public ProductService(string connectionString) => _conn = connectionString;

        public async Task<List<Product>> GetAllAsync(CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            return await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        }

        public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public async Task<Product> CreateAsync(Product product, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            await db.Products.AddAsync(product, ct);
            await db.SaveChangesAsync(ct);
            return product;
        }

        public async Task UpdateAsync(Product product, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            db.Products.Update(product);
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (entity is null) return;
            db.Products.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}
