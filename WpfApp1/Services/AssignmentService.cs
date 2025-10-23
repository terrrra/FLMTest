using FLMDesktop.Data;
using FLMDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly string _conn;
        public AssignmentService(string connectionString) => _conn = connectionString;

        public async Task<List<Product>> GetProductsForBranchAsync(int branchId, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            return await db.BranchProducts
                .Where(bp => bp.BranchId == branchId)
                .Select(bp => bp.Product)
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
        }

        public async Task SetAssignmentsAsync(int branchId, IEnumerable<int> productIds, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            using var tx = await db.Database.BeginTransactionAsync(ct);

            var existing = await db.BranchProducts.Where(x => x.BranchId == branchId).ToListAsync(ct);
            db.BranchProducts.RemoveRange(existing);

            var distinct = productIds.Distinct().ToArray();
            var toAdd = distinct.Select(pid => new BranchProduct { BranchId = branchId, ProductId = pid });
            await db.BranchProducts.AddRangeAsync(toAdd, ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task AddAssignmentAsync(int branchId, int productId, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            var exists = await db.BranchProducts.AnyAsync(x => x.BranchId == branchId && x.ProductId == productId, ct);
            if (!exists)
            {
                await db.BranchProducts.AddAsync(new BranchProduct { BranchId = branchId, ProductId = productId }, ct);
                await db.SaveChangesAsync(ct);
            }
        }

        public async Task RemoveAssignmentAsync(int branchId, int productId, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            var link = await db.BranchProducts.FirstOrDefaultAsync(x => x.BranchId == branchId && x.ProductId == productId, ct);
            if (link != null)
            {
                db.BranchProducts.Remove(link);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
