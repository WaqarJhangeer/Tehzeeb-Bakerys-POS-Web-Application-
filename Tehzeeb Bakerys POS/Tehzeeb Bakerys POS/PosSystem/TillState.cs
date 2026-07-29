using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PosSystem.Common;
using PosSystem.Data;
using PosSystem.Helpers;
using PosSystem.Models;
using PosSystem.Services;

namespace PosSystem.Web;

/// <summary>
/// The web equivalent of the console <c>Program</c>'s static fields: one till, shared by
/// every browser tab. All the rules still live in <see cref="Catalog"/>, <see cref="Order"/>
/// and the services - this class only sequences the calls and shapes the answer for JSON.
/// </summary>
public sealed class TillState
{
    /// <summary>Two HTTP requests must never mutate the same cart at once.</summary>
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

    private readonly Catalog _catalog = new Catalog();
    private readonly ReceiptPrinter _printer = new ReceiptPrinter();
    private readonly PaymentService _payments = new PaymentService();
    private readonly ProductImages _images;

    /// <summary>The sale currently open on the till, or null when no sale is running.</summary>
    private Order? _currentOrder;

    public TillState(ProductImages images)
    {
        _images = images;
    }

    // ---- startup ---------------------------------------------------------------------

    /// <summary>Same load the console app does at boot: catalog.json, or the seed list.</summary>
    public async Task<string> InitialiseAsync()
    {
        Result<List<Product>> result = await JsonStore.LoadCatalogAsync();

        if (result.IsFailure || result.Value is null)
        {
            return result.Message + " Starting with an empty catalog.";
        }

        int added = _catalog.LoadRange(result.Value);
        return $"{result.Message} {added} product(s) ready to sell.";
    }

    // ---- FR-1: catalog ---------------------------------------------------------------

    public async Task<ActionResponse> AddProductAsync(NewProductRequest request)
    {
        using (await LockAsync())
        {
            string sku = (request.Sku ?? string.Empty).Trim();
            string name = (request.Name ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(sku))
            {
                return Respond(false, "SKU is required.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Respond(false, "Product name is required.");
            }

            if (_catalog.TryFindBySku(sku, out Product? existing) && existing is not null)
            {
                return Respond(false, $"SKU {existing.Sku} is already used by '{existing.Name}'.");
            }

            if (request.Price < 0m)
            {
                return Respond(false, "Unit price cannot be negative.");
            }

            if (request.Stock < 0)
            {
                return Respond(false, "Opening stock cannot be negative.");
            }

            if (!Categories.Contains(request.Category))
            {
                return Respond(false, $"'{request.Category}' is not one of the Tehzeeb categories.");
            }

            string category = Categories.Normalise(request.Category);

            try
            {
                Result<Product> added = _catalog.AddProduct(
                    new Product(sku, name, request.Price, category, request.Stock));

                return Respond(added.IsSuccess, added.Message);
            }
            catch (ArgumentException ex)
            {
                return Respond(false, ex.Message);
            }
        }
    }

    public async Task<ActionResponse> SaveCatalogAsync()
    {
        using (await LockAsync())
        {
            Result<string> result = await JsonStore.SaveCatalogAsync(_catalog);
            return Respond(result.IsSuccess, result.Message);
        }
    }

    // ---- FR-2: start a sale ----------------------------------------------------------

    public async Task<ActionResponse> StartSaleAsync(StartSaleRequest request)
    {
        using (await LockAsync())
        {
            _currentOrder = new Order(request.CustomerName);
            return Respond(true, $"Opened {_currentOrder.OrderNumber} for {_currentOrder.CustomerName}.");
        }
    }

    /// <summary>Abandons the open sale without paying for it. Nothing was taken off stock.</summary>
    public async Task<ActionResponse> CancelSaleAsync()
    {
        using (await LockAsync())
        {
            if (_currentOrder is null)
            {
                return Respond(false, "No sale is open.");
            }

            string number = _currentOrder.OrderNumber;
            _currentOrder = null;
            return Respond(true, $"{number} was discarded. Nothing was charged.");
        }
    }

    // ---- FR-3: add to cart -----------------------------------------------------------

    public async Task<ActionResponse> AddToCartAsync(AddToCartRequest request)
    {
        using (await LockAsync())
        {
            if (_currentOrder is null)
            {
                return Respond(false, "No sale is open - start one first.");
            }

            if (!_catalog.TryFindBySku(request.Sku, out Product? product) || product is null)
            {
                return Respond(false, $"No product found for SKU '{request.Sku}'.");
            }

            if (request.Quantity <= 0)
            {
                return Respond(false, "Quantity must be at least 1.");
            }

            // Same rule as the console: a repeat SKU bumps the line, so check the combined total.
            CartItem? already = _currentOrder.FindLine(product.Sku);
            int wantedInTotal = request.Quantity + (already is null ? 0 : already.Quantity);

            if (!product.HasStockFor(wantedInTotal))
            {
                return Respond(false,
                    $"Only {product.StockQuantity} x {product.Name} in stock - cannot put {wantedInTotal} on this sale.");
            }

            _currentOrder.AddItem(product, request.Quantity);

            CartItem line = _currentOrder.FindLine(product.Sku)!;
            return Respond(true, $"Added {request.Quantity} x {line.Name} = {MoneyHelper.Format(line.LineTotal)}");
        }
    }

    // ---- FR-4: remove / undo ---------------------------------------------------------

    public async Task<ActionResponse> RemoveFromCartAsync(string? sku)
    {
        using (await LockAsync())
        {
            if (_currentOrder is null)
            {
                return Respond(false, "No sale is open.");
            }

            if (_currentOrder.RemoveItem(sku))
            {
                return Respond(true, $"Removed {(sku ?? string.Empty).ToUpperInvariant()} from the cart.");
            }

            return Respond(false, $"'{sku}' is not on this cart.");
        }
    }

    public async Task<ActionResponse> UndoLastAddAsync()
    {
        using (await LockAsync())
        {
            if (_currentOrder is null)
            {
                return Respond(false, "No sale is open.");
            }

            // out keyword: the Order tells us what it reversed.
            if (_currentOrder.UndoLastAdd(out string description))
            {
                return Respond(true, "Undone: " + description);
            }

            return Respond(false, "Nothing left to undo on this sale.");
        }
    }

    // ---- FR-6: checkout --------------------------------------------------------------

    public async Task<ActionResponse> CheckoutAsync(CheckoutRequest request)
    {
        using (await LockAsync())
        {
            if (_currentOrder is null)
            {
                return Respond(false, "No sale is open.");
            }

            Order order = _currentOrder;

            if (order.IsEmpty)
            {
                return Respond(false, "Cannot check out an empty cart.");
            }

            string method = (request.Method ?? string.Empty).Trim();
            if (!PaymentService.AvailableMethods.Contains(method, StringComparer.OrdinalIgnoreCase))
            {
                return Respond(false, $"'{request.Method}' is not a payment method this till accepts.");
            }

            // Re-check stock at the till: another sale may have drained it since it was added.
            foreach (CartItem line in order.Lines)
            {
                if (!line.Product.HasStockFor(line.Quantity))
                {
                    return Respond(false,
                        $"Stock problem: only {line.Product.StockQuantity} x {line.Name} left, cart wants {line.Quantity}. Adjust the cart before checking out.");
                }
            }

            decimal amount = order.CalculateTotal();

            // async / await: the till waits on the payment terminal without blocking a thread.
            Result<string> payment = await _payments.ProcessAsync(amount, method);
            if (payment.IsFailure)
            {
                return Respond(false, payment.Message);
            }

            // FR-6: only now is stock reduced. Product.ReduceStock passes its counter by `ref`.
            foreach (CartItem line in order.Lines)
            {
                line.Product.ReduceStock(line.Quantity);
            }

            order.MarkPaid(method, payment.ValueOr("-"));
            string receipt = order.BuildReceipt();

            // Queue<T>: the receipt joins the print spool, FIFO.
            _printer.Enqueue(order);

            // Task / await: archive the sale as JSON.
            Result<string> archived = await JsonStore.SaveOrderAsync(order);

            _currentOrder = null;

            // The full archive path is only worth saying when the write went wrong.
            string note = archived.IsSuccess
                ? $"Receipt queued for printing, {order.OrderNumber} archived."
                : archived.Message;

            return Respond(true, $"{payment.Message} {note}", receipt);
        }
    }

    // ---- support actions -------------------------------------------------------------

    public async Task<ActionResponse> FlushPrintQueueAsync()
    {
        using (await LockAsync())
        {
            if (_printer.PendingJobs == 0)
            {
                return Respond(false, "Print queue is empty.");
            }

            int printed = await _printer.FlushAsync();
            return Respond(true, $"{printed} receipt(s) written to {_printer.OutputFolder}.");
        }
    }

    public async Task<TillSnapshot> GetSnapshotAsync()
    {
        using (await LockAsync())
        {
            return BuildSnapshot();
        }
    }

    // ---- snapshot building -----------------------------------------------------------

    private ActionResponse Respond(bool ok, string message, string? receipt = null)
    {
        return new ActionResponse(ok, message, BuildSnapshot(), receipt);
    }

    private TillSnapshot BuildSnapshot()
    {
        string[] categories = Categories.All;
        Dictionary<string, string> images = _images.BuildLookup();

        ProductDto[] products = _catalog.Products
            .Select(p => new ProductDto(
                p.Sku,
                p.Name,
                p.Category,
                p.UnitPrice,
                MoneyHelper.Format(p.UnitPrice),
                p.StockQuantity,
                p.InStock,
                MoneyHelper.FormatPercent(TaxHelper.RateFor(p.Category)),
                images.TryGetValue(p.Sku, out string? url) ? url : null))
            .ToArray();

        TaxBracketDto[] brackets = categories
            .Select(c => new TaxBracketDto(c, TaxHelper.RateFor(c), MoneyHelper.FormatPercent(TaxHelper.RateFor(c))))
            .ToArray();

        return new TillSnapshot(
            products,
            categories,
            brackets,
            PaymentService.AvailableMethods,
            _catalog.SuggestNextSku(),
            _printer.PendingJobs,
            BuildOrderDto());
    }

    private OrderDto? BuildOrderDto()
    {
        if (_currentOrder is null)
        {
            return null;
        }

        Order order = _currentOrder;

        CartLineDto[] lines = order.Lines
            .Select(l => new CartLineDto(
                l.Sku,
                l.Name,
                l.Category,
                l.Quantity,
                l.UnitPrice,
                MoneyHelper.Format(l.UnitPrice),
                l.LineTotal,
                MoneyHelper.Format(l.LineTotal),
                l.Product.StockQuantity))
            .ToArray();

        decimal subtotal = order.CalculateSubtotal();
        decimal tax = order.CalculateTax();
        decimal total = order.CalculateTotal();

        return new OrderDto(
            order.OrderNumber,
            order.CustomerName,
            order.CreatedAt.ToString("HH:mm"),
            order.LineCount,
            order.TotalUnits,
            order.UndoDepth,
            order.IsEmpty,
            lines,
            subtotal,
            MoneyHelper.Format(subtotal),
            tax,
            MoneyHelper.Format(tax),
            total,
            MoneyHelper.Format(total));
    }

    // ---- the gate --------------------------------------------------------------------

    private async Task<IDisposable> LockAsync()
    {
        await _gate.WaitAsync();
        return new Releaser(_gate);
    }

    /// <summary>Private nested class: releases the semaphore when the `using` block ends.</summary>
    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        internal Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            _semaphore.Release();
        }
    }
}
