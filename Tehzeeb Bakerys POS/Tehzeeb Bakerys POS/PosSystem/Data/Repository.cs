using System;
using System.Collections.Generic;
using PosSystem.Models;

namespace PosSystem.Data;

/// <summary>
/// Generics: one reusable, type-safe in-memory store for any keyed entity.
/// Keeps a List&lt;T&gt; for ordering/iteration and a Dictionary&lt;string, T&gt;
/// alongside it for O(1) lookup by key (SKU / barcode).
/// </summary>
/// <typeparam name="T">Entity type - must expose a unique <see cref="IEntity.Key"/>.</typeparam>
public class Repository<T> where T : class, IEntity
{
    /// <summary>List&lt;T&gt;: insertion-ordered storage, used for listing.</summary>
    private readonly List<T> _items = new List<T>();

    /// <summary>Dictionary&lt;Key, Value&gt;: fast lookup by key, case-insensitive.</summary>
    private readonly Dictionary<string, T> _index =
        new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

    public int Count => _items.Count;

    public IReadOnlyList<T> Items => _items;

    public bool Add(T item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Key))
        {
            return false;
        }

        if (_index.ContainsKey(item.Key))
        {
            return false;
        }

        _items.Add(item);
        _index[item.Key] = item;
        return true;
    }

    /// <summary>out keyword: dictionary-style lookup that never throws.</summary>
    public bool TryGet(string? key, out T? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return _index.TryGetValue(key.Trim(), out item);
    }

    public T? Get(string? key)
    {
        return TryGet(key, out T? found) ? found : null;
    }

    public bool Contains(string? key)
    {
        return TryGet(key, out _);
    }

    public bool Remove(string? key)
    {
        if (!TryGet(key, out T? found) || found is null)
        {
            return false;
        }

        _items.Remove(found);
        _index.Remove(found.Key);
        return true;
    }

    public List<T> Where(Func<T, bool> predicate)
    {
        List<T> matches = new List<T>();
        foreach (T item in _items)
        {
            if (predicate(item))
            {
                matches.Add(item);
            }
        }

        return matches;
    }

    public void Clear()
    {
        _items.Clear();
        _index.Clear();
    }
}
