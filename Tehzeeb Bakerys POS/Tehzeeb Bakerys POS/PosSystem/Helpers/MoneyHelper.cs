using System;
using System.Globalization;

namespace PosSystem.Helpers;

/// <summary>
/// Static helper class: pure currency formatting/rounding, no object state.
/// </summary>
public static class MoneyHelper
{
    private const string CurrencySymbol = "Rs.";

    /// <summary>Rs. 1,450.00</summary>
    public static string Format(decimal amount)
    {
        return CurrencySymbol + " " + amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>Right-aligned money, used by the receipt columns.</summary>
    public static string FormatPadded(decimal amount, int width)
    {
        return Format(amount).PadLeft(width);
    }

    /// <summary>Bare number with no symbol - for tight table columns.</summary>
    public static string Bare(decimal amount)
    {
        return amount.ToString("N2", CultureInfo.InvariantCulture);
    }

    /// <summary>Money is always kept to 2 decimal places, rounding half away from zero.</summary>
    public static decimal Round(decimal amount)
    {
        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>0.05m -> "5%"</summary>
    public static string FormatPercent(decimal rate)
    {
        return (rate * 100m).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
