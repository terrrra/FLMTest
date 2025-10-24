using FLMDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace FLMDesktop.Data;

public class AppDbContext : DbContext
{
    private readonly string _connectionString;

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BranchProduct> BranchProducts => Set<BranchProduct>();

    public AppDbContext(string connectionString) => _connectionString = connectionString;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlServer(_connectionString, sql =>
        {
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sql.CommandTimeout(60);
        });
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Data/AppDbContext.cs  inside OnModelCreating(ModelBuilder model)
        model.Entity<Branch>().ToTable("Branch");
        model.Entity<Product>().ToTable("Product");

        model.Entity<BranchProduct>(e =>
        {
            e.ToTable("BranchProduct");
            e.HasKey(x => new { x.BranchId, x.ProductId });

            e.HasOne(x => x.Branch)
             .WithMany(b => b.BranchProducts)
             .HasForeignKey(x => x.BranchId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Product)
             .WithMany(p => p.BranchProducts)
             .HasForeignKey(x => x.ProductId)
             .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
