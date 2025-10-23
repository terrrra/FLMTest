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

        // ---------- Planning to make this as part of my repo publicly available once finished with it ----------
        public Task<int> ImportProductsAsync(string path) => ImportAsync(path, ImportKind.Products);
        public Task<int> ImportBranchesAsync(string path) => ImportAsync(path, ImportKind.Branches);
        public Task<int> ImportMappingsAsync(string path, int branchId) => ImportAsync(path, ImportKind.Mappings, branchId);

        public Task<int> ExportProductsAsync(string path) => ExportAsync(path, ExportKind.Products);
        public Task<int> ExportBranchesAsync(string path) => ExportAsync(path, ExportKind.Branches);
        public Task<int> ExportMappingsAsync(string path, int branchId) => ExportAsync(path, ExportKind.Mappings, branchId);

        // ---------- Import ----------
        private async Task<int> ImportAsync(string path, ImportKind kind, int branchId = 0)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            using var db = new AppDbContext(_conn);

            if (kind == ImportKind.Products)
            {
                var items = ext switch
                {
                    ".json" => ReadJson<ProductRow>(path),
                    ".xml" => ReadXml<ProductXmlWrapper, ProductRow>(path, x => x.Products),
                    ".csv" => ReadCsv<ProductRow>(path),
                    _ => throw new InvalidOperationException("Unsupported file type.")
                };

                int upserts = 0;
                foreach (var r in items)
                {
                    var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == r.ID);
                    if (entity == null)
                    {
                        entity = new Product { Id = r.ID };
                        db.Products.Add(entity);
                    }
                    entity.Name = r.Name ?? $"Product {r.ID}";
                    entity.WeightedItem = ParseBool(r.WeightedItem);
                    entity.SuggestedSellingPrice = ParseDecimal(r.SuggestedSellingPrice);

                    upserts++;
                }
                await db.SaveChangesAsync();
                return upserts;
            }

            if (kind == ImportKind.Branches)
            {
                var items = ext switch
                {
                    ".json" => ReadJson<BranchRow>(path),
                    ".xml" => ReadXml<BranchXmlWrapper, BranchRow>(path, x => x.Branches),
                    ".csv" => ReadCsv<BranchRow>(path),
                    _ => throw new InvalidOperationException("Unsupported file type.")
                };

                int upserts = 0;
                foreach (var r in items)
                {
                    var entity = await db.Branches.FirstOrDefaultAsync(b => b.Id == r.ID);
                    if (entity == null)
                    {
                        entity = new Branch { Id = r.ID };
                        db.Branches.Add(entity);
                    }
                    entity.Name = r.Name ?? $"Branch {r.ID}";
                    entity.TelephoneNumber = string.IsNullOrWhiteSpace(r.TelephoneNumber) ? null : r.TelephoneNumber;
                    entity.OpenDate = ParseDate(r.OpenDate) ?? DateTime.MinValue;

                    upserts++;
                }
                await db.SaveChangesAsync();
                return upserts;
            }

            // Mappings
            {
                var rows = ext switch
                {
                    ".json" => ReadJson<MappingRow>(path),
                    ".xml" => ReadXml<MappingXmlWrapper, MappingRow>(path, x => x.Mappings),
                    ".csv" => ReadCsv<MappingRow>(path),
                    _ => throw new InvalidOperationException("Unsupported file type.")
                };

                var toApply = branchId > 0 ? rows.Where(r => r.BranchID == branchId) : rows.Where(r => r.BranchID > 0);

                // use transaction; wipe & replace mappings for this branch when branchId specified
                using var tx = await db.Database.BeginTransactionAsync();
                int count = 0;

                if (branchId > 0)
                {
                    var existing = await db.BranchProducts.Where(x => x.BranchId == branchId).ToListAsync();
                    db.BranchProducts.RemoveRange(existing);
                    await db.SaveChangesAsync();
                }

                foreach (var r in toApply)
                {
                    // skip incomplete rows
                    if (r.BranchID <= 0 || r.ProductID <= 0) continue;

                    var exists = await db.BranchProducts.AnyAsync(x => x.BranchId == r.BranchID && x.ProductId == r.ProductID);
                    if (!exists)
                    {
                        await db.BranchProducts.AddAsync(new BranchProduct { BranchId = r.BranchID, ProductId = r.ProductID });
                        count++;
                    }
                }

                await db.SaveChangesAsync();
                await tx.CommitAsync();
                return count;
            }
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
                    }).ToListAsync();

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
        ?                          b.OpenDate.Value.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture)
        :                          string.Empty
                    }).ToListAsync();

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
