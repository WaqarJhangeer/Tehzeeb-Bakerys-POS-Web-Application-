using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PosSystem.Helpers;
using PosSystem.Models;

namespace PosSystem.Data;

/// <summary>
/// NuGet: uses Newtonsoft.Json to persist the catalog and completed orders.
/// Task / async / await: every call simulates real I/O latency and is awaited.
/// using keyword: StreamReader / StreamWriter are wrapped in `using` blocks so the
/// file handle is always released, even if writing throws.
/// </summary>
internal static class JsonStore
{
    private const string CatalogFileName = "catalog.json";
    private const string OrdersFolderName = "orders";

    internal static string DataFolder => Directory.GetCurrentDirectory();

    internal static string CatalogPath => Path.Combine(DataFolder, CatalogFileName);

    internal static string OrdersFolder => Path.Combine(DataFolder, OrdersFolderName);

    /// <summary>
    /// Loads the catalog in the background. Falls back to the seed products when
    /// catalog.json is missing or empty, so the till is never left with nothing to sell.
    /// </summary>
    internal static async Task<Result<List<Product>>> LoadCatalogAsync()
    {
        // Task: stand-in for a slow disk / network read.
        await Task.Delay(600);

        try
        {
            if (!File.Exists(CatalogPath))
            {
                return Result<List<Product>>.Ok(
                    SeedData.CreateDefaultProducts(),
                    $"No {CatalogFileName} found - loaded the default Tehzeeb product list.");
            }

            string json;
            using (StreamReader reader = new StreamReader(CatalogPath))
            {
                json = await reader.ReadToEndAsync();
            }

            List<Product>? products = JsonConvert.DeserializeObject<List<Product>>(json);
            if (products is null || products.Count == 0)
            {
                return Result<List<Product>>.Ok(
                    SeedData.CreateDefaultProducts(),
                    $"{CatalogFileName} was empty - loaded the default product list instead.");
            }

            return Result<List<Product>>.Ok(products, $"Loaded {products.Count} product(s) from {CatalogFileName}.");
        }
        catch (Exception ex)
        {
            return Result<List<Product>>.Fail($"Could not read {CatalogFileName}: {ex.Message}");
        }
    }

    /// <summary>Writes the whole catalog back out as indented JSON.</summary>
    internal static async Task<Result<string>> SaveCatalogAsync(Catalog catalog)
    {
        try
        {
            string json = JsonConvert.SerializeObject(catalog.Products, Formatting.Indented);

            using (StreamWriter writer = new StreamWriter(CatalogPath, false))
            {
                await writer.WriteAsync(json);
            }

            return Result<string>.Ok(CatalogPath, $"Saved {catalog.Count} product(s) to {CatalogPath}.");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Could not save {CatalogFileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// FR-6. Archives a completed sale as JSON. Serialising a flat snapshot keeps the
    /// file readable and avoids writing the Product objects out twice.
    /// </summary>
    internal static async Task<Result<string>> SaveOrderAsync(Order order)
    {
        try
        {
            Directory.CreateDirectory(OrdersFolder);
            string path = Path.Combine(OrdersFolder, order.OrderNumber + ".json");

            var snapshot = new
            {
                order.OrderNumber,
                order.CustomerName,
                order.CreatedAt,
                order.PaymentMethod,
                order.AuthCode,
                Lines = order.Lines.Select(line => new
                {
                    line.Sku,
                    line.Name,
                    line.Category,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal
                }).ToList(),
                Subtotal = order.CalculateSubtotal(),
                SalesTax = order.CalculateTax(),
                GrandTotal = order.CalculateTotal()
            };

            string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                await writer.WriteAsync(json);
            }

            return Result<string>.Ok(path, $"Order {order.OrderNumber} archived to {path}.");
        }
        catch (Exception ex)
        {
            return Result<string>.Fail($"Could not archive {order.OrderNumber}: {ex.Message}");
        }
    }
}
