using System;
using Newtonsoft.Json;
using PosSystem.Common;
using PosSystem.Helpers;

namespace PosSystem.Models;

/// <summary>
/// Classes &amp; objects: a sellable item in the Tehzeeb catalog - data plus behaviour.
/// Access modifiers: the mutable state is private; callers only see the public surface.
/// </summary>
public class Product : IEntity
{
    private readonly string _sku;
    private decimal _unitPrice;
    private int _stockQuantity;

    /// <summary>
    /// Constructor overload from the brief: Product(sku, name, price).
    /// Chains to the full constructor with sensible defaults.
    /// </summary>
    public Product(string sku, string name, decimal unitPrice)
        : this(sku, name, unitPrice, Categories.Default, 0)
    {
    }

    /// <summary>Full constructor. Also the one Newtonsoft.Json uses when loading catalog.json.</summary>
    [JsonConstructor]
    public Product(string sku, string name, decimal unitPrice, string category, int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        if (unitPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock cannot be negative.");
        }

        _sku = sku.Trim().ToUpperInvariant();
        _unitPrice = MoneyHelper.Round(unitPrice);
        _stockQuantity = stockQuantity;
        Name = name.Trim();
        Category = Categories.Normalise(category);
    }

    public string Sku => _sku;

    public string Name { get; private set; }

    public string Category { get; private set; }

    public decimal UnitPrice => _unitPrice;

    public int StockQuantity => _stockQuantity;

    /// <summary>The repository key for this entity is its SKU. Not persisted to JSON.</summary>
    [JsonIgnore]
    public string Key => _sku;

    [JsonIgnore]
    public bool InStock => _stockQuantity > 0;

    /// <summary>Method: repricing goes through here so rounding is never skipped.</summary>
    public bool ChangePrice(decimal newPrice)
    {
        if (newPrice < 0m)
        {
            return false;
        }

        _unitPrice = MoneyHelper.Round(newPrice);
        return true;
    }

    public bool HasStockFor(int quantity)
    {
        return quantity > 0 && quantity <= _stockQuantity;
    }

    /// <summary>
    /// Hands the private stock field to the helper by <c>ref</c> so it is decremented in place.
    /// </summary>
    public bool ReduceStock(int quantity)
    {
        return InventoryHelper.TryReduce(ref _stockQuantity, quantity);
    }

    /// <summary>ref again - the helper bumps this object's own counter.</summary>
    public void Restock(int quantity)
    {
        InventoryHelper.Increase(ref _stockQuantity, quantity);
    }

    /// <summary>One fixed-width row for the catalog table.</summary>
    public string ToTableRow()
    {
        return _sku.PadRight(10)
             + Truncate(Name, 26).PadRight(28)
             + Category.PadRight(15)
             + MoneyHelper.Format(_unitPrice).PadLeft(13)
             + _stockQuantity.ToString().PadLeft(7);
    }

    public override string ToString()
    {
        return $"{_sku} - {Name} @ {MoneyHelper.Format(_unitPrice)} ({_stockQuantity} in stock)";
    }

    // Private helper - nobody outside Product needs this.
    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text.Substring(0, max - 1) + ".";
    }
}
