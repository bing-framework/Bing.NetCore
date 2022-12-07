namespace Bing.Caching;

/// <summary>
/// 基于本地内存的列表缓存基类
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class ListLocalMemoryBase<T> : IListCache<T>
{
    /// <summary>
    /// 缓存键数量
    /// </summary>
    public int Count => GetCache().Count;

    /// <summary>
    /// 读取全部
    /// </summary>
    public virtual IList<T> ReaderAll() => GetCache();

    /// <summary>
    /// 清空
    /// </summary>
    public virtual void Clear()
    {
        lock (GetSyncCache())
            GetCache().Clear();
    }

    /// <summary>
    /// 添加。如果存在则不添加，返回false
    /// </summary>
    /// <param name="item">列表项</param>
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

    /// <summary>
    /// 移除。如果存在则删除并返回true，否则返回false
    /// </summary>
    /// <param name="item">列表项</param>
    public virtual bool Remove(T item)
    {
        if (!Exists(item))
            return false;
        lock (GetSyncCache())
            return GetCache().Remove(item);
    }

    /// <summary>
    /// 判断是否存在
    /// </summary>
    /// <param name="item">列表项</param>
    public virtual bool Exists(T item) => GetCache().Contains(item);

    /// <summary>
    /// 获取缓存
    /// </summary>
    protected abstract IList<T> GetCache();

    /// <summary>
    /// 获取同步的缓存对象。用于线程安全
    /// </summary>
    protected abstract object GetSyncCache();
}
