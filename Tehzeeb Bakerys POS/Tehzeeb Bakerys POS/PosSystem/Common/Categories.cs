using System;

namespace PosSystem.Common;

/// <summary>
/// Fixed lookup of the product categories Tehzeeb sells under.
/// Demonstrates: static class + arrays + the `out` keyword.
/// </summary>
internal static class Categories
{
    /// <summary>Array: a fixed, ordered lookup table. Index 0 is the default.</summary>
    private static readonly string[] Names =
    {
        "Bakery",
        "Cakes",
        "Savouries",
        "Confectionery",
        "Beverages"
    };

    internal const string Default = "Bakery";

    /// <summary>Hands out a copy so callers cannot mutate the master array.</summary>
    internal static string[] All => (string[])Names.Clone();

    internal static int Count => Names.Length;

    internal static bool Contains(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        for (int i = 0; i < Names.Length; i++)
        {
            if (string.Equals(Names[i], category.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Maps free-text input onto a known category, falling back to the default.</summary>
    internal static string Normalise(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Default;
        }

        string trimmed = category.Trim();
        for (int i = 0; i < Names.Length; i++)
        {
            if (string.Equals(Names[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return Names[i];
            }
        }

        return Default;
    }

    /// <summary>
    /// out keyword: returns success plus the resolved category in one call.
    /// </summary>
    internal static bool TryGetByNumber(int oneBasedIndex, out string category)
    {
        category = Default;
        if (oneBasedIndex < 1 || oneBasedIndex > Names.Length)
        {
            return false;
        }

        category = Names[oneBasedIndex - 1];
        return true;
    }

    internal static string Menu()
    {
        string[] all = All;
        string[] parts = new string[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            parts[i] = $"{i + 1}) {all[i]}";
        }

        return string.Join("   ", parts);
    }
}
