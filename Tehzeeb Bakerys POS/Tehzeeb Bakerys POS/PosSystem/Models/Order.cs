using System;
using System.Collections.Generic;
using System.Text;
using PosSystem.Helpers;

namespace PosSystem.Models;

/// <summary>
/// Classes &amp; objects: an open sale (the cart) that becomes a completed order at checkout.
/// Demonstrates: List&lt;T&gt; for the line items, Stack&lt;T&gt; for undo history,
/// StringBuilder for the receipt, and `ref` for the running tax total.
/// </summary>
public class Order
{
    private const int ReceiptWidth = 48;

    // Static field shared by every Order object - gives each sale a unique number.
    private static int _lastOrderNumber = 1000;

    /// <summary>List&lt;T&gt;: the cart line items, in the order the cashier scanned them.</summary>
    private readonly List<CartItem> _lines = new List<CartItem>();

    /// <summary>Stack&lt;T&gt;: last-in-first-out action history powering "undo last item".</summary>
    private readonly Stack<CartAction> _history = new Stack<CartAction>();

    private readonly string _customerName;

    /// <summary>Constructor: opening a new sale for a customer (FR-2).</summary>
    public Order(string? customerName)
    {
        _lastOrderNumber++;
        OrderNumber = "SO-" + _lastOrderNumber;
        _customerName = string.IsNullOrWhiteSpace(customerName) ? "Walk-in Customer" : customerName.Trim();
        CreatedAt = DateTime.Now;
        PaymentMethod = "Unpaid";
        AuthCode = "-";
    }

    public string OrderNumber { get; }

    public string CustomerName => _customerName;

    public DateTime CreatedAt { get; }

    public bool IsCompleted { get; private set; }

    public string PaymentMethod { get; private set; }

    public string AuthCode { get; private set; }

    /// <summary>Read-only view - callers cannot add lines behind the Order's back.</summary>
    public IReadOnlyList<CartItem> Lines => _lines;

    public int LineCount => _lines.Count;

    public bool IsEmpty => _lines.Count == 0;

    /// <summary>
    /// How many undo steps would actually do something. History entries whose line was
    /// already removed outright are stale and get skipped by <see cref="UndoLastAdd"/>.
    /// </summary>
    public int UndoDepth
    {
        get
        {
            int usable = 0;
            foreach (CartAction action in _history)
            {
                if (FindLine(action.Sku) is not null)
                {
                    usable++;
                }
            }

            return usable;
        }
    }

    public int TotalUnits
    {
        get
        {
            int units = 0;
            foreach (CartItem line in _lines)
            {
                units += line.Quantity;
            }

            return units;
        }
    }

    /// <summary>
    /// FR-3. Method: adds a product by SKU/quantity; an SKU already on the cart just
    /// gets its quantity increased. Every add is pushed onto the undo stack.
    /// </summary>
    public void AddItem(Product product, int quantity)
    {
        if (product is null)
        {
            throw new ArgumentNullException(nameof(product));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1.");
        }

        CartItem? existing = FindLine(product.Sku);
        if (existing is null)
        {
            _lines.Add(new CartItem(product, quantity));
        }
        else
        {
            existing.IncreaseQuantity(quantity);
        }

        _history.Push(new CartAction(product.Sku, quantity, $"{quantity} x {product.Name}"));
    }

    /// <summary>FR-4. Removes a whole line item by SKU.</summary>
    public bool RemoveItem(string? sku)
    {
        CartItem? line = FindLine(sku);
        if (line is null)
        {
            return false;
        }

        _lines.Remove(line);
        return true;
    }

    /// <summary>
    /// FR-4. Pops the undo stack and reverses the most recent add.
    /// out keyword: hands back a description of what was undone.
    /// Stale entries (whose line was already removed outright) are skipped.
    /// </summary>
    public bool UndoLastAdd(out string description)
    {
        description = string.Empty;

        while (_history.Count > 0)
        {
            CartAction action = _history.Pop();
            CartItem? line = FindLine(action.Sku);
            if (line is null)
            {
                continue;
            }

            if (line.Quantity <= action.QuantityAdded)
            {
                _lines.Remove(line);
            }
            else
            {
                line.DecreaseQuantity(action.QuantityAdded);
            }

            description = action.Description;
            return true;
        }

        return false;
    }

    public CartItem? FindLine(string? sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        string wanted = sku.Trim();
        foreach (CartItem line in _lines)
        {
            if (string.Equals(line.Sku, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }

        return null;
    }

    /// <summary>FR-5. Method: sum of every line total, before tax.</summary>
    public decimal CalculateSubtotal()
    {
        decimal subtotal = 0m;
        foreach (CartItem line in _lines)
        {
            subtotal += line.LineTotal;
        }

        return MoneyHelper.Round(subtotal);
    }

    /// <summary>FR-5. Tax is per-category, accumulated in place via `ref`.</summary>
    public decimal CalculateTax()
    {
        decimal runningTax = 0m;
        foreach (CartItem line in _lines)
        {
            TaxHelper.AccumulateTax(line.LineTotal, line.Category, ref runningTax);
        }

        return runningTax;
    }

    /// <summary>FR-5. Method: the grand total the customer pays.</summary>
    public decimal CalculateTotal()
    {
        return MoneyHelper.Round(CalculateSubtotal() + CalculateTax());
    }

    /// <summary>FR-6. Stamps the sale as paid and closed.</summary>
    public void MarkPaid(string method, string authCode)
    {
        PaymentMethod = string.IsNullOrWhiteSpace(method) ? "Cash" : method;
        AuthCode = string.IsNullOrWhiteSpace(authCode) ? "-" : authCode;
        IsCompleted = true;
    }

    /// <summary>StringBuilder: the on-screen cart view the cashier works from.</summary>
    public string BuildCartView()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("Order ").Append(OrderNumber)
          .Append("   Customer: ").Append(_customerName)
          .Append("   Opened: ").AppendLine(CreatedAt.ToString("HH:mm"));

        if (IsEmpty)
        {
            sb.AppendLine("  (cart is empty - use 'Add item to cart')");
            return sb.ToString();
        }

        sb.AppendLine(new string('-', 66));
        sb.Append("#".PadRight(4))
          .Append("SKU".PadRight(10))
          .Append("ITEM".PadRight(24))
          .Append("QTY".PadLeft(4))
          .Append("UNIT".PadLeft(11))
          .AppendLine("AMOUNT".PadLeft(13));
        sb.AppendLine(new string('-', 66));

        for (int i = 0; i < _lines.Count; i++)
        {
            CartItem line = _lines[i];
            sb.Append((i + 1).ToString().PadRight(4))
              .Append(line.Sku.PadRight(10))
              .Append(Fit(line.Name, 23).PadRight(24))
              .Append(line.Quantity.ToString().PadLeft(4))
              .Append(MoneyHelper.Bare(line.UnitPrice).PadLeft(11))
              .AppendLine(MoneyHelper.Bare(line.LineTotal).PadLeft(13));
        }

        sb.AppendLine(new string('-', 66));
        sb.Append("Subtotal".PadRight(52)).AppendLine(MoneyHelper.Format(CalculateSubtotal()).PadLeft(14));
        sb.Append("Sales tax".PadRight(52)).AppendLine(MoneyHelper.Format(CalculateTax()).PadLeft(14));
        sb.Append("GRAND TOTAL".PadRight(52)).AppendLine(MoneyHelper.Format(CalculateTotal()).PadLeft(14));

        return sb.ToString();
    }

    /// <summary>
    /// StringBuilder: assembles the multi-line receipt in one buffer instead of
    /// concatenating dozens of throwaway strings.
    /// </summary>
    public string BuildReceipt()
    {
        StringBuilder sb = new StringBuilder();
        string rule = new string('=', ReceiptWidth);
        string thin = new string('-', ReceiptWidth);

        sb.AppendLine(rule);
        sb.AppendLine(Centre("TEHZEEB BAKERS"));
        sb.AppendLine(Centre("Point of Sale"));
        sb.AppendLine(Centre("All amounts in PKR"));
        sb.AppendLine(rule);
        sb.Append("Order    : ").AppendLine(OrderNumber);
        sb.Append("Customer : ").AppendLine(_customerName);
        sb.Append("Date     : ").AppendLine(CreatedAt.ToString("dd-MMM-yyyy HH:mm"));
        sb.Append("Payment  : ").Append(PaymentMethod).Append("  [").Append(AuthCode).AppendLine("]");
        sb.AppendLine(thin);

        sb.Append("ITEM".PadRight(24))
          .Append("QTY".PadLeft(4))
          .Append("UNIT".PadLeft(9))
          .AppendLine("AMOUNT".PadLeft(11));
        sb.AppendLine(thin);

        foreach (CartItem line in _lines)
        {
            sb.Append(Fit(line.Name, 23).PadRight(24))
              .Append(line.Quantity.ToString().PadLeft(4))
              .Append(MoneyHelper.Bare(line.UnitPrice).PadLeft(9))
              .AppendLine(MoneyHelper.Bare(line.LineTotal).PadLeft(11));
        }

        sb.AppendLine(thin);
        sb.Append("Subtotal".PadRight(34)).AppendLine(MoneyHelper.Format(CalculateSubtotal()).PadLeft(14));
        sb.Append("Sales tax".PadRight(34)).AppendLine(MoneyHelper.Format(CalculateTax()).PadLeft(14));
        sb.AppendLine(thin);
        sb.Append("GRAND TOTAL".PadRight(34)).AppendLine(MoneyHelper.Format(CalculateTotal()).PadLeft(14));
        sb.AppendLine(rule);
        sb.AppendLine(Centre($"Items: {TotalUnits}   Lines: {LineCount}"));
        sb.AppendLine(Centre("Thank you for shopping at Tehzeeb!"));
        sb.AppendLine(rule);

        return sb.ToString();
    }

    public override string ToString()
    {
        return $"{OrderNumber} - {_customerName} - {LineCount} line(s) - {MoneyHelper.Format(CalculateTotal())}";
    }

    // ---- private formatting helpers -------------------------------------------------

    private static string Centre(string text)
    {
        if (text.Length >= ReceiptWidth)
        {
            return text;
        }

        int padLeft = (ReceiptWidth - text.Length) / 2;
        return new string(' ', padLeft) + text;
    }

    private static string Fit(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text.Substring(0, max - 1) + ".";
    }
}
