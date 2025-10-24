using FLMDesktop.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Services
{
    public interface IAssignmentService
    {
        Task<List<Product>> GetProductsForBranchAsync(int branchId, CancellationToken ct = default);
        Task SetAssignmentsAsync(int branchId, IEnumerable<int> productIds, CancellationToken ct = default);
        Task AddAssignmentAsync(int branchId, int productId, CancellationToken ct = default);
        Task RemoveAssignmentAsync(int branchId, int productId, CancellationToken ct = default);
    }
}
