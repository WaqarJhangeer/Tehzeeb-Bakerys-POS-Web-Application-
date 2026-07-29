namespace PosSystem.Helpers;

/// <summary>
/// Internal helper: adjusts a stock counter in place.
/// Demonstrates: `internal` access modifier + the `ref` keyword operating on a
/// caller's private field (see <see cref="Models.Product.ReduceStock"/>).
/// </summary>
internal static class InventoryHelper
{
    /// <summary>
    /// ref keyword: decrements the caller's own stock field when the sale is possible.
    /// Returns false and leaves the counter untouched when it is not.
    /// </summary>
    internal static bool TryReduce(ref int stockOnHand, int quantity)
    {
        if (quantity <= 0 || quantity > stockOnHand)
        {
            return false;
        }

        stockOnHand -= quantity;
        return true;
    }

    /// <summary>ref keyword: increments the caller's stock field in place.</summary>
    internal static void Increase(ref int stockOnHand, int quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        stockOnHand += quantity;
    }
}
