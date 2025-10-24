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
        public async Task AssignAsync(int branchId, int productId)
        {
            using var db = new AppDbContext(_conn);

            var exists = await db.BranchProducts
                .AnyAsync(bp => bp.BranchId == branchId && bp.ProductId == productId);
            if (exists) return;

            db.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = productId });
            await db.SaveChangesAsync();
        }

        public async Task SetAssignmentsAsync(
    int branchId, IEnumerable<int> productIds, CancellationToken ct = default)
        {
            // ✅ Set Assignments will safely assign products to Items
            var targetIds = (productIds ?? Enumerable.Empty<int>()).Distinct().ToArray();

            using var db = new AppDbContext(_conn);
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // keep only product IDs that actually exist
                var validIds = await db.Products
                    .Where(p => targetIds.Contains(p.Id))
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                var currentIds = await db.BranchProducts
                    .Where(bp => bp.BranchId == branchId)
                    .Select(bp => bp.ProductId)
                    .ToListAsync(ct);

                var toAdd = validIds.Except(currentIds).ToArray();
                var toRemove = currentIds.Except(validIds).ToArray();

                if (toAdd.Length > 0)
                {
                    var links = toAdd.Select(pid => new BranchProduct { BranchId = branchId, ProductId = pid });
                    await db.BranchProducts.AddRangeAsync(links, ct);
                }

#if NET8_0_OR_GREATER
                if (toRemove.Length > 0)
                    await db.BranchProducts
                        .Where(bp => bp.BranchId == branchId && toRemove.Contains(bp.ProductId))
                        .ExecuteDeleteAsync(ct);
#else
        if (toRemove.Length > 0)
        {
            var links = await db.BranchProducts
                .Where(bp => bp.BranchId == branchId && toRemove.Contains(bp.ProductId))
                .ToListAsync(ct);
            db.BranchProducts.RemoveRange(links);
        }
#endif

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
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
