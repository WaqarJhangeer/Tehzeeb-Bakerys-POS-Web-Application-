using System;
using System.Threading.Tasks;
using PosSystem.Helpers;

namespace PosSystem.Services;


/// Task / async / await: stands in for a card terminal. Checkout awaits
/// <see cref="ProcessAsync"/> so the UI thread is never blocked by the "network" call.

public class PaymentService
{
    /// <summary>Array: the fixed list of tender types the till accepts.</summary>
    private static readonly string[] Methods =
    {
        "Cash",
        "Debit Card",
        "Credit Card",
        "Easypaisa"
    };

    private readonly Random _random = new Random();

    public static string[] AvailableMethods => (string[])Methods.Clone();

    /// out keyword: resolves a 1-based menu choice to a tender type.
    public static bool TryGetMethod(int oneBasedIndex, out string method)
    {
        method = Methods[0];
        if (oneBasedIndex < 1 || oneBasedIndex > Methods.Length)
        {
            return false;
        }

        method = Methods[oneBasedIndex - 1];
        return true;
    }

    
    /// Authorises the payment asynchronously and returns the auth code on success.
    ///
    public async Task<Result<string>> ProcessAsync(decimal amount, string method)
    {
        if (amount <= 0m)
        {
            return Result<string>.Fail("There is nothing to charge.");
        }

        // await: yields while the "terminal" thinks about it.
        await Task.Delay(1200);

        if (string.Equals(method, "Cash", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string>.Ok("CASH", $"Cash payment of {MoneyHelper.Format(amount)} taken.");
        }

        string authCode = "AUTH-" + _random.Next(100000, 999999).ToString();
        return Result<string>.Ok(authCode, $"{method} payment of {MoneyHelper.Format(amount)} approved.");
    }
}
