namespace PosSystem.Models;

/// <summary>
/// Anything the generic <see cref="Data.Repository{T}"/> can store must expose a
/// unique string key. For a <see cref="Product"/> that key is its SKU.
/// </summary>
public interface IEntity
{
    string Key { get; }
}
