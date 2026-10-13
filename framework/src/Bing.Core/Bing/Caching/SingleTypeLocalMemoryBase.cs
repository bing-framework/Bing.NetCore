namespace Bing.Caching;

/// <summary>
/// 为基于本地内存字典的单一键值类型缓存提供基础实现。
/// </summary>
/// <typeparam name="TKey">缓存键类型。</typeparam>
/// <typeparam name="TValue">缓存值类型。</typeparam>
public abstract class SingleTypeLocalMemoryBase<TKey, TValue> : ISingleTypeCache<TKey, TValue>
{
    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (GetSyncCache())
                return GetCache().Count;
        }
    }

    /// <inheritdoc />
    /// <remarks>键不存在时返回 <typeparamref name="TValue"/> 的默认值。</remarks>
    public virtual TValue Get(TKey key)
    {
        lock (GetSyncCache())
            return GetCache().TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>
    /// 清空缓存中的所有键值对。
    /// </summary>
    public virtual void Clear()
    {
        lock (GetSyncCache())
            GetCache().Clear();
    }

    /// <inheritdoc />
    /// <remarks>返回读取时的字典快照；快照中的值对象不会被深度复制。</remarks>
    public virtual IDictionary<TKey, TValue> Reader()
    {
        lock (GetSyncCache())
            return new Dictionary<TKey, TValue>(GetCache());
    }

    /// <inheritdoc />
    public virtual bool Add(TKey key, TValue value)
    {
        lock (GetSyncCache())
        {
            var cache = GetCache();
            if (cache.TryGetValue(key, out _))
                return false;
            cache.Add(key, value);
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Update(TKey key, TValue value)
    {
        lock (GetSyncCache())
        {
            var cache = GetCache();
            if (!cache.TryGetValue(key, out _))
                return false;
            cache[key] = value;
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Set(TKey key, TValue value)
    {
        lock (GetSyncCache())
        {
            GetCache()[key] = value;
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Remove(TKey key)
    {
        lock (GetSyncCache())
            return GetCache().Remove(key);
    }

    /// <inheritdoc />
    /// <remarks>返回是否至少移除了一个键。</remarks>
    public virtual bool Remove(TKey[] keys)
    {
        lock (GetSyncCache())
        {
            var removed = false;
            var cache = GetCache();
            foreach (var key in keys)
                removed |= cache.Remove(key);
            return removed;
        }
    }

    /// <inheritdoc />
    public virtual bool Exists(TKey key)
    {
        lock (GetSyncCache())
            return GetCache().TryGetValue(key, out _);
    }

    /// <summary>
    /// 获取当前缓存使用的内部可变字典。
    /// </summary>
    /// <returns>缓存键值对的内部字典。</returns>
    protected abstract IDictionary<TKey, TValue> GetCache();

    /// <summary>
    /// 获取用于保护内部字典写操作的同步对象。
    /// </summary>
    /// <returns>派生类稳定持有的同步对象。</returns>
    protected abstract object GetSyncCache();
}
