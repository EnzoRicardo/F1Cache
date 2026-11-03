namespace F1Cache.Core.Interfaces;

public interface ICache<TKey, TValue>
    where TKey : notnull
{
    void Set(TKey key, TValue item);

    bool TryGet(TKey key, out TValue result);

    bool Remove(TKey key);

    void Clear();
}
