using System;
using System.Text;

namespace PosSystem.Helpers;

/// <summary>
/// Static helper class holding the sales-tax rules.
/// Demonstrates: static class + arrays (tax brackets) + the `ref` keyword.
/// </summary>
public static class TaxHelper
{
    /// <summary>Arrays: two index-aligned lookup tables form the tax-bracket table.</summary>
    private static readonly string[] BracketCategories =
    {
        "Bakery",
        "Cakes",
        "Savouries",
        "Confectionery",
        "Beverages"
    };

    private static readonly decimal[] BracketRates =
    {
        0.00m, // plain bread / rusk is zero-rated
        0.05m,
        0.05m,
        0.10m,
        0.16m  // packaged drinks carry the full rate
    };

    public const decimal FallbackRate = 0.05m;

    public static decimal RateFor(string? category)
    {
        if (!string.IsNullOrWhiteSpace(category))
        {
            for (int i = 0; i < BracketCategories.Length; i++)
            {
                if (string.Equals(BracketCategories[i], category.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return BracketRates[i];
                }
            }
        }

        return FallbackRate;
    }

    public static decimal CalculateTax(decimal amount, string? category)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return MoneyHelper.Round(amount * RateFor(category));
    }

    /// <summary>
    /// ref keyword: adds this line's tax straight into the caller's running total,
    /// so <see cref="Models.Order"/> never has to reassign the accumulator itself.
    /// </summary>
    public static void AccumulateTax(decimal lineAmount, string? category, ref decimal runningTax)
    {
        runningTax = MoneyHelper.Round(runningTax + CalculateTax(lineAmount, category));
    }

    /// <summary>StringBuilder: renders the bracket table for the console.</summary>
    public static string DescribeBrackets()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Sales tax brackets");
        sb.AppendLine("------------------------------");
        for (int i = 0; i < BracketCategories.Length; i++)
        {
            sb.Append("  ")
              .Append(BracketCategories[i].PadRight(20))
              .AppendLine(MoneyHelper.FormatPercent(BracketRates[i]).PadLeft(6));
        }

        return sb.ToString();
    }
}
