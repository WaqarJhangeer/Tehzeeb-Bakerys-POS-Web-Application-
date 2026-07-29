using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace PosSystem.Web;

/// <summary>
/// Finds the picture for a product by convention rather than by storing a path on the
/// <see cref="PosSystem.Models.Product"/>: a file in wwwroot/images named after the SKU
/// (TB-1005.jpg) is that product's photo. Anything without a file falls back to the
/// built-in category artwork drawn in the browser.
/// </summary>
public sealed class ProductImages
{
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif", ".svg"
    };

    private readonly string _folder;

    public ProductImages(IWebHostEnvironment environment)
    {
        string webRoot = environment.WebRootPath ?? Directory.GetCurrentDirectory();
        _folder = Path.Combine(webRoot, "images");
    }

    /// <summary>
    /// One directory listing per snapshot, keyed by SKU. Cheap (a single call, not one
    /// per product) and it means a photo dropped in while the till is running shows up
    /// on the next refresh - no restart needed.
    /// </summary>
    public Dictionary<string, string> BuildLookup()
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(_folder))
        {
            return map;
        }

        foreach (string path in Directory.EnumerateFiles(_folder))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(AllowedExtensions, extension) < 0)
            {
                continue;
            }

            string sku = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(sku) || map.ContainsKey(sku))
            {
                continue;
            }

            map[sku] = "/images/" + Uri.EscapeDataString(Path.GetFileName(path));
        }

        return map;
    }
}
