using System;
using PosSystem.Helpers;

namespace PosSystem.Models;

/// <summary>
/// Classes &amp; objects: one line on the till - a product, how many, and what it costs.
/// The unit price is frozen when the line is created so a later price change on the
/// catalog product cannot rewrite a sale that is already in progress.
/// </summary>
public class CartItem
{
    private readonly Product _product;
    private int _quantity;

    public CartItem(Product product, int quantity)
    {
        if (product is null)
        {
            throw new ArgumentNullException(nameof(product));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1.");
        }

        _product = product;
        _quantity = quantity;
        UnitPrice = product.UnitPrice;
    }

    public Product Product => _product;

    public string Sku => _product.Sku;

    public string Name => _product.Name;

    public string Category => _product.Category;

    /// <summary>Price captured at the moment this line was opened.</summary>
    public decimal UnitPrice { get; }

    public int Quantity => _quantity;

    public decimal LineTotal => MoneyHelper.Round(UnitPrice * _quantity);

    /// <summary>Method: FR-3 - adding an SKU that is already on the cart bumps the quantity.</summary>
    public void IncreaseQuantity(int by)
    {
        if (by <= 0)
        {
            return;
        }

        _quantity += by;
    }

    /// <summary>Method: used by "undo last item" to peel back the quantity that was just added.</summary>
    public bool DecreaseQuantity(int by)
    {
        if (by <= 0 || by >= _quantity)
        {
            return false;
        }

        _quantity -= by;
        return true;
    }

    public override string ToString()
    {
        return $"{_quantity} x {Name} = {MoneyHelper.Format(LineTotal)}";
    }
}
