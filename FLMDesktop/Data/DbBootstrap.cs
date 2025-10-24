using FLMDesktop.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FLMDesktop.Data;

public static class DbBootstrap
{
       //I created this class to assist whith testing the db connection and to load to the grid on startup. This will change and probably not be used later
    public static async Task EnsureReadyAsync(string connectionString, CancellationToken ct = default)
    {
        var csb = new SqlConnectionStringBuilder(connectionString);
        var dbName = csb.InitialCatalog;
        csb.InitialCatalog = "master";

        // wait for SQL to be reachable
        for (int i = 0; i < 10; i++)
        {
            try { using var c = new SqlConnection(csb.ConnectionString); await c.OpenAsync(ct); break; }
            catch { await Task.Delay(1500, ct); }
        }

        // create DB if missing
        using (var c = new SqlConnection(csb.ConnectionString))
        {
            await c.OpenAsync(ct);
            using var cmd = new SqlCommand($"IF DB_ID(@db) IS NULL EXEC('CREATE DATABASE [' + @db + ']');", c);
            cmd.Parameters.AddWithValue("@db", dbName);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // create tables if missing
        using var db = new AppDbContext(connectionString);
        await db.Database.EnsureCreatedAsync(ct);

        // (optional) seed sample branches once
        if (!await db.Branches.AnyAsync(ct))
        {
            db.Branches.AddRange(
                new Branch { Name = "CBD", TelephoneNumber = "0111234567", OpenDate = new DateTime(2021, 1, 15) },
                new Branch { Name = "Sandton", TelephoneNumber = "0117654321", OpenDate = new DateTime(2022, 5, 1) },
                new Branch { Name = "Hatfield", TelephoneNumber = "0121112222", OpenDate = new DateTime(2023, 3, 10) },
                new Branch { Name = "Sunnyside", TelephoneNumber = "0123334444", OpenDate = new DateTime(2023, 9, 1) }
            );
            await db.SaveChangesAsync(ct);
        }
    }
}
