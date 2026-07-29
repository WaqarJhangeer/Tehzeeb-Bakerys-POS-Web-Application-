using System.Collections.Generic;
using PosSystem.Models;

namespace PosSystem.Data;

/// <summary>
/// Internal helper: the starter catalog used the first time the app runs
/// (i.e. before catalog.json exists on disk).
/// </summary>
internal static class SeedData
{
    internal static List<Product> CreateDefaultProducts()
    {
        return new List<Product>
        {
            new Product("TB-1001", "Chicken Patties",          150m,  "Savouries",     40),
            new Product("TB-1002", "Chicken Bread",            320m,  "Savouries",     15),
            new Product("TB-1003", "Bran Bread (Large)",       180m,  "Bakery",        25),
            new Product("TB-1004", "Milk Rusk 500g",           420m,  "Bakery",        30),
            new Product("TB-1005", "Chocolate Fudge Cake 1lb", 1450m, "Cakes",          8),
            new Product("TB-1006", "Black Forest Cake 2lb",    2600m, "Cakes",          5),
            new Product("TB-1007", "Dry Cake Slice",           90m,   "Bakery",        60),
            new Product("TB-1008", "Almond Cookies 400g",      650m,  "Confectionery", 20),
            new Product("TB-1009", "Mineral Water 1.5L",       100m,  "Beverages",     50),
            new Product("TB-1010", "Fresh Cream Roll",         120m,  "Bakery",        35),
            new Product("TB-1011", "Cold Coffee",              350m,  "Beverages",     25)
        };
    }
}
