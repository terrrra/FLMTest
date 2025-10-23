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
        public BranchService(string connectionString) => _conn = connectionString;

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

        public async Task<Branch> CreateAsync(Branch branch, CancellationToken ct = default)
        {
            Validate(branch);

            using var db = new AppDbContext(_conn);

            // Make absolutely sure EF treats this as a new row
            branch.Id = 0;
            db.Entry(branch).State = EntityState.Added;

            await db.SaveChangesAsync(ct);
            return branch;
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
