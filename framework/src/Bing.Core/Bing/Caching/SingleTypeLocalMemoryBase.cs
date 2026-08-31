namespace Bing.Caching;

/// <summary>
/// 为基于本地内存字典的单一键值类型缓存提供基础实现。
/// </summary>
/// <typeparam name="TKey">缓存键类型。</typeparam>
/// <typeparam name="TValue">缓存值类型。</typeparam>
public abstract class SingleTypeLocalMemoryBase<TKey, TValue> : ISingleTypeCache<TKey, TValue>
{
    /// <inheritdoc />
    public int Count => GetCache().Count;

    /// <inheritdoc />
    /// <remarks>键不存在时返回 <typeparamref name="TValue"/> 的默认值。</remarks>
    public virtual TValue Get(TKey key) => Exists(key) ? GetCache()[key] : default;

    /// <summary>
    /// 清空缓存中的所有键值对。
    /// </summary>
    public virtual void Clear()
    {
        lock (GetSyncCache())
            GetCache().Clear();
    }

    /// <inheritdoc />
    /// <remarks>直接返回内部可变字典；调用方修改该字典会绕过当前类的同步控制。</remarks>
    public virtual IDictionary<TKey, TValue> Reader() => GetCache();

    /// <inheritdoc />
    /// <remarks>并发竞争导致重复键异常时会被忽略，方法仍返回 <c>true</c>。</remarks>
    public virtual bool Add(TKey key, TValue value)
    {
        if (Exists(key))
            return false;
        lock (GetSyncCache())
        {
            try
            {
                GetCache().Add(key, value);
            }
            catch (ArgumentException)
            {
                // 忽略添加相同键的异常，为了预防密集的线程
            }
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Update(TKey key, TValue value)
    {
        if (!Exists(key))
            return false;
        lock (GetSyncCache())
        {
            GetCache()[key] = value;
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Set(TKey key, TValue value)
    {
        if (Exists(key))
            return Update(key, value);
        return Add(key, value);
    }

    /// <inheritdoc />
    public virtual bool Remove(TKey key)
    {
        if (!Exists(key))
            return false;
        lock (GetSyncCache())
            return GetCache().Remove(key);
    }

    /// <inheritdoc />
    /// <remarks>逐个尝试移除键，当前实现始终返回 <c>true</c>。</remarks>
    public virtual bool Remove(TKey[] keys)
    {
        foreach (var key in keys)
            Remove(key);
        return true;
    }

    /// <inheritdoc />
    public virtual bool Exists(TKey key) => GetCache().ContainsKey(key);

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
