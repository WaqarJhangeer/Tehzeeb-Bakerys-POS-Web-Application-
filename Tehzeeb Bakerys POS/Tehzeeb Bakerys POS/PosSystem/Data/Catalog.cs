using System;
using System.Collections.Generic;
using System.Text;
using PosSystem.Helpers;
using PosSystem.Models;

namespace PosSystem.Data;

/// <summary>
/// FR-1. The product catalog. Wraps the generic <see cref="Repository{T}"/> so the
/// rest of the app talks in POS terms (SKU, category) rather than storage terms.
/// </summary>
public class Catalog
{
    private readonly Repository<Product> _repository = new Repository<Product>();

    public int Count => _repository.Count;

    public IReadOnlyList<Product> Products => _repository.Items;

    /// <summary>Generics in action: the add either succeeds with the product or explains why not.</summary>
    public Result<Product> AddProduct(Product product)
    {
        if (product is null)
        {
            return Result<Product>.Fail("No product supplied.");
        }

        if (_repository.Contains(product.Sku))
        {
            return Result<Product>.Fail($"SKU {product.Sku} already exists in the catalog.");
        }

        _repository.Add(product);
        return Result<Product>.Ok(product, $"Added {product.Name} ({product.Sku}).");
    }

    /// <summary>Method from the brief: Catalog.FindBySku() - O(1) via the dictionary index.</summary>
    public Product? FindBySku(string? sku)
    {
        return _repository.Get(sku);
    }

    /// <summary>out keyword: the non-throwing variant used by the menu handlers.</summary>
    public bool TryFindBySku(string? sku, out Product? product)
    {
        return _repository.TryGet(sku, out product);
    }

    public List<Product> InCategory(string category)
    {
        return _repository.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
    }

    public List<Product> LowStock(int threshold)
    {
        return _repository.Where(p => p.StockQuantity <= threshold);
    }

    /// <summary>Bulk load (used after the async JSON/seed load). Duplicate SKUs are skipped.</summary>
    public int LoadRange(IEnumerable<Product> products)
    {
        int added = 0;
        foreach (Product product in products)
        {
            if (_repository.Add(product))
            {
                added++;
            }
        }

        return added;
    }

    /// <summary>Suggests the next free SKU in the TB-#### series.</summary>
    public string SuggestNextSku()
    {
        int highest = 1000;
        foreach (Product product in _repository.Items)
        {
            if (product.Sku.StartsWith("TB-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(product.Sku.Substring(3), out int number)
                && number > highest)
            {
                highest = number;
            }
        }

        return "TB-" + (highest + 1);
    }

    /// <summary>StringBuilder: renders the whole catalog as one fixed-width table.</summary>
    public string BuildProductTable()
    {
        StringBuilder sb = new StringBuilder();
        string rule = new string('-', 73);

        sb.AppendLine(rule);
        sb.Append("SKU".PadRight(10))
          .Append("NAME".PadRight(28))
          .Append("CATEGORY".PadRight(15))
          .Append("PRICE".PadLeft(13))
          .AppendLine("STOCK".PadLeft(7));
        sb.AppendLine(rule);

        if (_repository.Count == 0)
        {
            sb.AppendLine("  (catalog is empty)");
            sb.AppendLine(rule);
            return sb.ToString();
        }

        foreach (Product product in _repository.Items)
        {
            sb.AppendLine(product.ToTableRow());
        }

        sb.AppendLine(rule);
        sb.Append("  ").Append(_repository.Count).AppendLine(" product(s) in catalog.");

        return sb.ToString();
    }
}
