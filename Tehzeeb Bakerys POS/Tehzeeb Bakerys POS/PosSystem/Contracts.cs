namespace PosSystem.Web;

// ---- what the browser sends up ------------------------------------------------------

/// <summary>FR-2. Body of POST /api/sale.</summary>
public sealed record StartSaleRequest(string? CustomerName);

/// <summary>FR-1. Body of POST /api/products.</summary>
public sealed record NewProductRequest(string? Sku, string? Name, decimal Price, string? Category, int Stock);

/// <summary>FR-3. Body of POST /api/cart/items.</summary>
public sealed record AddToCartRequest(string? Sku, int Quantity);

/// <summary>FR-6. Body of POST /api/checkout.</summary>
public sealed record CheckoutRequest(string? Method);

// ---- what the browser gets back -----------------------------------------------------

/// <summary>
/// Every endpoint answers in this shape: did it work, what should the cashier be told,
/// and the whole till state afterwards so the page can just re-render.
/// </summary>
public sealed record ActionResponse(bool Ok, string Message, TillSnapshot State, string? Receipt = null);

public sealed record TillSnapshot(
    ProductDto[] Catalog,
    string[] Categories,
    TaxBracketDto[] TaxBrackets,
    string[] PaymentMethods,
    string SuggestedSku,
    int PrintQueue,
    OrderDto? Order);

public sealed record ProductDto(
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    string UnitPriceText,
    int StockQuantity,
    bool InStock,
    string TaxRateText,
    /// <summary>wwwroot/images/{SKU}.{ext} if one exists; null means "draw the category artwork".</summary>
    string? ImageUrl);

public sealed record TaxBracketDto(string Category, decimal Rate, string RateText);

public sealed record OrderDto(
    string OrderNumber,
    string CustomerName,
    string OpenedAt,
    int LineCount,
    int TotalUnits,
    int UndoDepth,
    bool IsEmpty,
    CartLineDto[] Lines,
    decimal Subtotal,
    string SubtotalText,
    decimal Tax,
    string TaxText,
    decimal Total,
    string TotalText);

public sealed record CartLineDto(
    string Sku,
    string Name,
    string Category,
    int Quantity,
    decimal UnitPrice,
    string UnitPriceText,
    decimal LineTotal,
    string LineTotalText,
    int StockQuantity);
