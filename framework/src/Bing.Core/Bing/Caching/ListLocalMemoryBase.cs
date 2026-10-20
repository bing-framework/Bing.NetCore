namespace Bing.Caching;

/// <summary>
/// 为基于本地内存集合的列表缓存提供基础实现。
/// </summary>
/// <typeparam name="T">缓存项目的类型。</typeparam>
public abstract class ListLocalMemoryBase<T> : IListCache<T>
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
    /// <remarks>返回读取时的列表快照；快照中的项目不会被深度复制。</remarks>
    public virtual IList<T> ReaderAll()
    {
        lock (GetSyncCache())
            return new List<T>(GetCache());
    }

    /// <summary>
    /// 清空缓存中的全部项目。
    /// </summary>
    public virtual void Clear()
    {
        lock (GetSyncCache())
            GetCache().Clear();
    }

    /// <inheritdoc />
    public virtual bool Add(T item)
    {
        lock (GetSyncCache())
        {
            var cache = GetCache();
            if (cache.Contains(item))
                return false;
            cache.Add(item);
            return true;
        }
    }

    /// <inheritdoc />
    public virtual bool Remove(T item)
    {
        lock (GetSyncCache())
            return GetCache().Remove(item);
    }

    /// <inheritdoc />
    public virtual bool Exists(T item)
    {
        lock (GetSyncCache())
            return GetCache().Contains(item);
    }

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
