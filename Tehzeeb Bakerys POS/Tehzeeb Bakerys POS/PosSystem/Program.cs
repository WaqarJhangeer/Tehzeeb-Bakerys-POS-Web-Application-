using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosSystem.Web;

// The web front-end for the Tehzeeb Bakers till. Same rules as the console app -
// this file is only plumbing that maps HTTP requests onto TillState.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// One till for the whole shop, shared by every browser tab.
builder.Services.AddSingleton<ProductImages>();
builder.Services.AddSingleton<TillState>();

WebApplication app = builder.Build();

// Load catalog.json (or the seed list) before the first request arrives.
TillState till = app.Services.GetRequiredService<TillState>();
Console.WriteLine(await till.InitialiseAsync());

// wwwroot/index.html is the till screen. The page uses a few non-ASCII glyphs, so the
// charset is spelled out rather than left to the browser to sniff.
FileExtensionContentTypeProvider contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".html"] = "text/html; charset=utf-8";
contentTypes.Mappings[".css"] = "text/css; charset=utf-8";
contentTypes.Mappings[".js"] = "text/javascript; charset=utf-8";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

RouteGroupBuilder api = app.MapGroup("/api");

// FR-1 + FR-5: everything the screen needs to draw itself.
api.MapGet("/state", async (TillState state) => Results.Ok(await state.GetSnapshotAsync()));

// FR-1: register a new SKU, price, category and opening stock.
api.MapPost("/products", async (NewProductRequest request, TillState state) =>
    Reply(await state.AddProductAsync(request)));

// Write catalog.json via Newtonsoft.Json.
api.MapPost("/catalog/save", async (TillState state) => Reply(await state.SaveCatalogAsync()));

// FR-2: open an empty cart for a customer.
api.MapPost("/sale", async (StartSaleRequest request, TillState state) =>
    Reply(await state.StartSaleAsync(request)));

api.MapDelete("/sale", async (TillState state) => Reply(await state.CancelSaleAsync()));

// FR-3: add by SKU + quantity (a repeat SKU bumps the quantity).
api.MapPost("/cart/items", async (AddToCartRequest request, TillState state) =>
    Reply(await state.AddToCartAsync(request)));

// FR-4: drop a whole line item.
api.MapDelete("/cart/items/{sku}", async (string sku, TillState state) =>
    Reply(await state.RemoveFromCartAsync(sku)));

// FR-4: reverse the most recent add (Stack).
api.MapPost("/cart/undo", async (TillState state) => Reply(await state.UndoLastAddAsync()));

// FR-6: async payment, reduce stock, receipt, JSON archive.
api.MapPost("/checkout", async (CheckoutRequest request, TillState state) =>
    Reply(await state.CheckoutAsync(request)));

// Drain the FIFO print spool to receipts/*.txt.
api.MapPost("/print/flush", async (TillState state) => Reply(await state.FlushPrintQueueAsync()));

app.Run();

// A refused action is still a 200 - the page always wants the fresh till state back,
// and `ok: false` is what turns the toast red.
static IResult Reply(ActionResponse response)
{
    return Results.Ok(response);
}
