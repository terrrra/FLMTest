using CsvHelper;
using CsvHelper.Configuration;
using FLMDesktop.Data;
using FLMDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace FLMDesktop.Services
{
    public class ImportExportService
    {
        private readonly string _conn;
        public ImportExportService(string connectionString) => _conn = connectionString;

        // ---------- Planning to make this a generic Bulk File Importer. This is why the Persistance Logic resides here ----------
        public Task<int> ImportProductsAsync(string path)=> ImportProductsCoreAsync(path, Path.GetExtension(path).ToLowerInvariant());
        public Task<int> ImportBranchesAsync(string path) => ImportBranchesCoreAsync(path, Path.GetExtension(path).ToLowerInvariant());
        public Task<int> ImportMappingsAsync(string path, int branchId)=> ImportMappingsCoreAsync(path, Path.GetExtension(path).ToLowerInvariant(), branchId);

        public Task<int> ExportProductsAsync(string path) => ExportAsync(path, ExportKind.Products);
        public Task<int> ExportBranchesAsync(string path) => ExportAsync(path, ExportKind.Branches);
        public Task<int> ExportMappingsAsync(string path, int branchId) => ExportAsync(path, ExportKind.Mappings, branchId);

        // ---------- Import ----------
        private async Task<int> ImportProductsCoreAsync(string path, string ext)
        {
            var rows = ext switch
            {
                ".json" => ReadJson<ProductRow>(path),
                ".xml" => ReadXml<ProductXmlWrapper, ProductRow>(path, x => x.Products),
                ".csv" => ReadCsv<ProductRow>(path),
                _ => throw new InvalidOperationException("Unsupported file type.")
            };

            using var db = new AppDbContext(_conn);
            await db.Database.OpenConnectionAsync();

            int count = 0;
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                var incomingIds = rows.Where(r => r.ID > 0).Select(r => r.ID).Distinct().ToList();
                var existingIds = await db.Products.AsNoTracking()
                    .Where(p => incomingIds.Contains(p.Id))
                    .Select(p => p.Id).ToListAsync();

                // UPDATE existing
                foreach (var r in rows)
                {
                    var p = await db.Products.FirstOrDefaultAsync(x => x.Id == r.ID);
                    if (p is null) continue;
                    p.Name = r.Name ?? p.Name;
                    p.WeightedItem = ParseBool(r.WeightedItem);
                    p.SuggestedSellingPrice = ParseDecimal(r.SuggestedSellingPrice);
                    count++;
                }
                await db.SaveChangesAsync();

                // INSERT without Id
                foreach (var r in rows.Where(x => x.ID == 0))
                {
                    db.Products.Add(new Product
                    {
                        Name = r.Name ?? "Product",
                        WeightedItem = ParseBool(r.WeightedItem),
                        SuggestedSellingPrice = ParseDecimal(r.SuggestedSellingPrice)
                    });
                    count++;
                }
                await db.SaveChangesAsync();

                // INSERT with explicit Ids
                var newExplicitIds = incomingIds.Except(existingIds).ToList();
                if (newExplicitIds.Count > 0)
                {
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Product] ON");

                    foreach (var r in rows.Where(x => x.ID > 0 && newExplicitIds.Contains(x.ID)))
                    {
                        db.Products.Add(new Product
                        {
                            Id = r.ID,
                            Name = r.Name ?? $"Product {r.ID}",
                            WeightedItem = ParseBool(r.WeightedItem),
                            SuggestedSellingPrice = ParseDecimal(r.SuggestedSellingPrice)
                        });
                        count++;
                    }

                    await db.SaveChangesAsync();
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Product] OFF");
                }

                await tx.CommitAsync();
            });

            await db.Database.CloseConnectionAsync();
            return count;
        }
        private async Task<int> ImportMappingsCoreAsync(string path, string ext, int branchId = 0)
        {
            var rows = ext switch
            {
                ".json" => ReadJson<MappingRow>(path),
                ".xml" => ReadXml<MappingXmlWrapper, MappingRow>(path, x => x.Mappings),
                ".csv" => ReadCsv<MappingRow>(path),
                _ => throw new InvalidOperationException("Unsupported file type.")
            };

            using var db = new AppDbContext(_conn);
            var strategy = db.Database.CreateExecutionStrategy();

            int count = 0;
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                var input = branchId > 0 ? rows.Where(r => r.BranchID == branchId) : rows;

                foreach (var r in input)
                {
                    bool branchExists = await db.Branches.AnyAsync(b => b.Id == r.BranchID);
                    bool productExists = await db.Products.AnyAsync(p => p.Id == r.ProductID);
                    if (!branchExists || !productExists) continue;

                    bool already = await db.BranchProducts.AnyAsync(x => x.BranchId == r.BranchID && x.ProductId == r.ProductID);
                    if (already) continue;

                    db.BranchProducts.Add(new BranchProduct { BranchId = r.BranchID, ProductId = r.ProductID });
                    count++;
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
            });

            return count;
        }
        private async Task<int> ImportBranchesCoreAsync(string path, string ext)
        {
            var rows = ext switch
            {
                ".json" => ReadJson<BranchRow>(path),
                ".xml" => ReadXml<BranchXmlWrapper, BranchRow>(path, x => x.Branches),
                ".csv" => ReadCsv<BranchRow>(path),
                _ => throw new InvalidOperationException("Unsupported file type.")
            };

            using var db = new AppDbContext(_conn);
            await db.Database.OpenConnectionAsync();

            int count = 0;
            var strategy = db.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                var incomingIds = rows.Where(r => r.ID > 0).Select(r => r.ID).Distinct().ToList();
                var existingIds = await db.Branches.AsNoTracking()
                    .Where(b => incomingIds.Contains(b.Id))
                    .Select(b => b.Id).ToListAsync();

                // UPDATE existing
                foreach (var r in rows)
                {
                    var b = await db.Branches.FirstOrDefaultAsync(x => x.Id == r.ID);
                    if (b is null) continue;

                    b.Name = r.Name ?? b.Name;
                    b.TelephoneNumber = string.IsNullOrWhiteSpace(r.TelephoneNumber) ? null : r.TelephoneNumber;
                    b.OpenDate = ParseDate(r.OpenDate);
                    count++;
                }
                await db.SaveChangesAsync();

                // INSERT without Id (let SQL generate)
                foreach (var r in rows.Where(x => x.ID == 0))
                {
                    db.Branches.Add(new Branch
                    {
                        Name = r.Name ?? "Branch",
                        TelephoneNumber = string.IsNullOrWhiteSpace(r.TelephoneNumber) ? null : r.TelephoneNumber,
                        OpenDate = ParseDate(r.OpenDate)
                    });
                    count++;
                }
                await db.SaveChangesAsync();

                // INSERT with explicit Ids
                var newExplicitIds = incomingIds.Except(existingIds).ToList();
                if (newExplicitIds.Count > 0)
                {
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Branch] ON");

                    foreach (var r in rows.Where(x => x.ID > 0 && newExplicitIds.Contains(x.ID)))
                    {
                        db.Branches.Add(new Branch
                        {
                            Id = r.ID,
                            Name = r.Name ?? $"Branch {r.ID}",
                            TelephoneNumber = string.IsNullOrWhiteSpace(r.TelephoneNumber) ? null : r.TelephoneNumber,
                            OpenDate = ParseDate(r.OpenDate)
                        });
                        count++;
                    }

                    await db.SaveChangesAsync();
                    await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[Branch] OFF");
                }

                await tx.CommitAsync();
            });

            await db.Database.CloseConnectionAsync();
            return count;
        }


        // ---------- Export ----------
        private async Task<int> ExportAsync(string path, ExportKind kind, int branchId = 0)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            using var db = new AppDbContext(_conn);

            if (kind == ExportKind.Products)
            {
                var data = await db.Products.AsNoTracking()
                    .OrderBy(p => p.Id)
                    .Select(p => new ProductRow
                    {
                        ID = p.Id,
                        Name = p.Name,
                        WeightedItem = p.WeightedItem ? "Y" : "N",
                        SuggestedSellingPrice = p.SuggestedSellingPrice
                            .ToString("0.##", CultureInfo.InvariantCulture)
                    })
                    .ToListAsync();

                WriteByExt(path, ext, data, new ProductXmlWrapper { Products = data });
                return data.Count;
            }

            if (kind == ExportKind.Branches)
            {
                var data = await db.Branches.AsNoTracking()
                    .OrderBy(b => b.Id)
                    .Select(b => new BranchRow
                    {
                        ID = b.Id,
                        Name = b.Name,
                        TelephoneNumber = b.TelephoneNumber,
                        OpenDate = b.OpenDate.HasValue
                            ? b.OpenDate.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture)
                            : string.Empty
                    })
                    .ToListAsync();

                WriteByExt(path, ext, data, new BranchXmlWrapper { Branches = data });
                return data.Count;
            }

            // mappings
            {
                var q = db.BranchProducts.AsNoTracking();
                if (branchId > 0) q = q.Where(x => x.BranchId == branchId);

                var data = await q.OrderBy(x => x.BranchId).ThenBy(x => x.ProductId)
                    .Select(x => new MappingRow { BranchID = x.BranchId, ProductID = x.ProductId })
                    .ToListAsync();

                WriteByExt(path, ext, data, new MappingXmlWrapper { Mappings = data });
                return data.Count;
            }
        }

        // ---------- Helpers ----------
        private static bool ParseBool(string? raw)
        {
            var s = (raw ?? "").Trim().ToLowerInvariant();
            return s is "y" or "1" or "true";
        }
        private static async Task SetIdentityInsertAsync(DbContext db, string table, bool on)
        {
            var sql = $"SET IDENTITY_INSERT [dbo].[{table}] {(on ? "ON" : "OFF")}";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        private static decimal ParseDecimal(object? raw)
        {
            // handles "", ".", ".99", "9.99"
            if (raw == null) return 0m;
            var s = raw.ToString()!.Trim();
            if (string.IsNullOrEmpty(s) || s == ".") return 0m;
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            return 0m;
        }

        private static DateTime? ParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim();
            string[] fmts = { "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy" };
            if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(s, out dt)) return dt;
            return null;
        }

        private static List<T> ReadJson<T>(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        private static List<T> ReadCsv<T>(string path)
        {
            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                BadDataFound = null
            };
            using var sr = new StreamReader(path, Encoding.UTF8);
            using var csv = new CsvReader(sr, cfg);
            return csv.GetRecords<T>().ToList();
        }

        private static List<TItem> ReadXml<TWrapper, TItem>(string path, Func<TWrapper, List<TItem>> pick)
        {
            var ser = new XmlSerializer(typeof(TWrapper));
            using var fs = File.OpenRead(path);
            var obj = (TWrapper?)ser.Deserialize(fs);
            return obj is null ? new() : (pick(obj) ?? new());
        }

        private static void WriteByExt<T, TWrapper>(string path, string ext, List<T> rows, TWrapper wrapper)
        {
            switch (ext)
            {
                case ".csv":
                    var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
                    using (var sw = new StreamWriter(path, false, Encoding.UTF8))
                    using (var csv = new CsvWriter(sw, cfg))
                        csv.WriteRecords(rows);
                    break;
                case ".json":
                    File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
                    break;
                case ".xml":
                    var ser = new XmlSerializer(typeof(TWrapper));
                    using (var fs = File.Create(path))
                        ser.Serialize(fs, wrapper!);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported file type.");
            }
        }

        // ---------- DTOs that mirror the files ----------
        public sealed class ProductRow
        {
            public int ID { get; set; }
            public string? Name { get; set; }
            public string? WeightedItem { get; set; }// "Y"/"N"/"0"/"" -> bool
            public object? SuggestedSellingPrice { get; set; }// "", ".", 9.99
        }

        [XmlRoot("Products")]
        public class ProductXmlWrapper
        {
            [XmlElement("Product")]
            public List<ProductRow> Products { get; set; } = new();
        }

        public sealed class BranchRow
        {
            public int ID { get; set; }
            public string? Name { get; set; }
            public string? TelephoneNumber { get; set; }
            public string? OpenDate { get; set; }// "yyyy/MM/dd" etc.
        }
        [XmlRoot("Branches")]
        public class BranchXmlWrapper { public List<BranchRow> Branches { get; set; } = new(); }

        public sealed class MappingRow
        {
            public int BranchID { get; set; }
            public int ProductID { get; set; }
        }
        [XmlRoot("Mappings")]
        public class MappingXmlWrapper { public List<MappingRow> Mappings { get; set; } = new(); }

        private enum ImportKind { Products, Branches, Mappings }
        private enum ExportKind { Products, Branches, Mappings }
    }
}
