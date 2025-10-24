using FLMDesktop.Data;
using FLMDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

//If you followed the docker db steps, Make sure that the db image is running in the container, this should make this flow. Because of time...
namespace FLMDesktop.Services
{
    public class BranchService : IBranchService
    {
        private readonly string _conn;
        private readonly IAssignmentService _assignments;
        public BranchService(string connectionString,IAssignmentService? assignments = null)
        {
            _conn = connectionString;
            _assignments = assignments ?? new AssignmentService(_conn);
        }

        public async Task<List<Branch>> GetAllAsync(CancellationToken ct = default)
        {
           
                using var db = new AppDbContext(_conn);
                return await db.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        }

        public async Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            return await db.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
        }

        public async Task<int> CreateAsync(Branch draft, CancellationToken ct = default)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));

            // Normalize inputs
            draft.Name = (draft.Name ?? "").Trim();
            if (draft.Name.Length == 0)
                throw new InvalidOperationException("Branch name is required.");

            using var db = new AppDbContext(_conn);
            db.Branches.Add(draft);
            await db.SaveChangesAsync(ct);          // draft.Id is generated here

            // If caller passed any initial assignments, apply them safely
            var productIds =
                draft.BranchProducts?.Select(bp => bp.ProductId)  // might be null
                ?? Enumerable.Empty<int>();                       // never null

            if (productIds.Any())
                await _assignments.SetAssignmentsAsync(draft.Id, productIds, ct);

            return draft.Id;
        }

        public async Task UpdateAsync(Branch branch, CancellationToken ct = default)
        {
            Validate(branch);
            using var db = new AppDbContext(_conn);
            db.Branches.Update(branch);
            await db.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            using var db = new AppDbContext(_conn);
            var entity = await db.Branches.FirstOrDefaultAsync(b => b.Id == id, ct);
            if (entity is null) return;
            db.Branches.Remove(entity);
            await db.SaveChangesAsync(ct);
        }

        private static void Validate(Branch b)
        {
            if (string.IsNullOrWhiteSpace(b.Name))
                throw new ArgumentException("Branch.Name is required.");
            if (b.TelephoneNumber is not { Length: 10 } && b.TelephoneNumber.All(char.IsLetter))
                throw new ArgumentException("Telephone number must be 10 digits with no letters.");
        }
    }
}
