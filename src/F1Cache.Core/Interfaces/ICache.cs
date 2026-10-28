namespace F1Cache.Core.Interfaces;

public interface ICache<TKey, TValue>
    where TKey : notnull
{
    void Set(TKey key, TValue value);

    bool TryGet(TKey key, out TValue value);

    bool Remove(TKey key);

    void Clear();
}