namespace Bing.Caching;

/// <summary>
/// 为基于本地内存集合的列表缓存提供基础实现。
/// </summary>
/// <typeparam name="T">缓存项目的类型。</typeparam>
public abstract class ListLocalMemoryBase<T> : IListCache<T>
{
    /// <inheritdoc />
    public int Count => GetCache().Count;

    /// <inheritdoc />
    /// <remarks>直接返回内部可变列表；调用方修改该列表会绕过当前类的同步控制。</remarks>
    public virtual IList<T> ReaderAll() => GetCache();

    /// <summary>
    /// 清空缓存中的全部项目。
    /// </summary>
    public virtual void Clear()
    {
        lock (GetSyncCache())
            GetCache().Clear();
    }

    /// <inheritdoc />
    /// <remarks>存在性检查与后续添加不构成单个原子操作，派生类应提供稳定的同步对象。</remarks>
    public virtual bool Add(T item)
    {
        if (Exists(item))
            return false;
        lock (GetSyncCache())
        {
            GetCache().Add(item);
            return true;
        }
    }

    /// <inheritdoc />
    /// <remarks>存在性检查与后续移除不构成单个原子操作。</remarks>
    public virtual bool Remove(T item)
    {
        if (!Exists(item))
            return false;
        lock (GetSyncCache())
            return GetCache().Remove(item);
    }

    /// <inheritdoc />
    public virtual bool Exists(T item) => GetCache().Contains(item);

    /// <summary>
    /// 获取当前缓存使用的内部可变列表。
    /// </summary>
    /// <returns>缓存项目的内部列表。</returns>
    protected abstract IList<T> GetCache();

    /// <summary>
    /// 获取用于保护内部列表写操作的同步对象。
    /// </summary>
    /// <returns>派生类稳定持有的同步对象。</returns>
    protected abstract object GetSyncCache();
}
