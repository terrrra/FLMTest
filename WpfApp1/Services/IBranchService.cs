using FLMDesktop.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Services
{
    public interface IBranchService
    {
        Task<List<Branch>> GetAllAsync(CancellationToken ct = default);
        Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<int> CreateAsync(Branch branch, CancellationToken ct = default);
        Task UpdateAsync(Branch branch, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
