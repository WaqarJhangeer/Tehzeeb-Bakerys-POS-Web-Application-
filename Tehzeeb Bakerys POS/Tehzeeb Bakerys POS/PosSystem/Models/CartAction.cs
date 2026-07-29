using System;

namespace PosSystem.Models;

/// <summary>
/// One entry in the cart's action history. Pushed onto a <see cref="System.Collections.Generic.Stack{T}"/>
/// by <see cref="Order.AddItem"/> so the most recent add can be reversed (FR-4).
/// </summary>
internal sealed class CartAction
{
    internal CartAction(string sku, int quantityAdded, string description)
    {
        Sku = sku;
        QuantityAdded = quantityAdded;
        Description = description;
        HappenedAt = DateTime.Now;
    }

    internal string Sku { get; }

    internal int QuantityAdded { get; }

    internal string Description { get; }

    internal DateTime HappenedAt { get; }

    public override string ToString()
    {
        return $"{HappenedAt:HH:mm:ss}  added {Description}";
    }
}
