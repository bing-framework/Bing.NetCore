namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 具有可选容量限制的线程安全惰性值缓存。
/// </summary>
/// <typeparam name="TKey">缓存键类型。</typeparam>
/// <typeparam name="TValue">缓存值类型。</typeparam>
internal sealed class BoundedLazyCache<TKey, TValue>
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<TKey, CacheEntry> _entries = new();
    private readonly LinkedList<TKey> _accessOrder = new();
    private long _hitCount;
    private long _missCount;
    private long _bypassCount;
    private long _evictionCount;

    /// <summary>
    /// 初始化一个 <see cref="BoundedLazyCache{TKey,TValue}"/> 类型的实例。
    /// </summary>
    /// <param name="capacity">缓存容量；<see langword="null"/> 表示无上限，<c>0</c> 表示不缓存。</param>
    public BoundedLazyCache(int? capacity = null)
    {
        if (capacity.HasValue && capacity.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "缓存容量不能小于 0。");
        Capacity = capacity;
    }

    /// <summary>
    /// 缓存容量。
    /// </summary>
    public int? Capacity { get; }

    /// <summary>
    /// 当前缓存条目数。
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncRoot)
                return _entries.Count;
        }
    }

    /// <summary>
    /// 缓存命中次数。
    /// </summary>
    public long HitCount
    {
        get
        {
            lock (_syncRoot)
                return _hitCount;
        }
    }

    /// <summary>
    /// 缓存未命中次数。
    /// </summary>
    public long MissCount
    {
        get
        {
            lock (_syncRoot)
                return _missCount;
        }
    }

    /// <summary>
    /// 因容量为零而不缓存的次数。
    /// </summary>
    public long BypassCount
    {
        get
        {
            lock (_syncRoot)
                return _bypassCount;
        }
    }

    /// <summary>
    /// 最久未使用条目被移除的次数。
    /// </summary>
    public long EvictionCount
    {
        get
        {
            lock (_syncRoot)
                return _evictionCount;
        }
    }

    /// <summary>
    /// 获取已缓存惰性值，或创建并按容量策略缓存新值。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="factory">创建惰性值的工厂。</param>
    /// <returns>当前调用应使用的惰性值。</returns>
    public Lazy<TValue> GetOrAdd(TKey key, Func<Lazy<TValue>> factory)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                _hitCount++;
                Touch(entry);
                return entry.Value;
            }

            _missCount++;
            var value = factory();
            if (Capacity == 0)
            {
                _bypassCount++;
                return value;
            }

            if (Capacity.HasValue && _entries.Count >= Capacity.Value)
                EvictLeastRecentlyUsed();
            var node = _accessOrder.AddLast(key);
            _entries.Add(key, new CacheEntry(value, node));
            return value;
        }
    }

    /// <summary>
    /// 仅在缓存仍持有指定惰性值时移除该项。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">本次调用观察到的惰性值。</param>
    public void RemoveIfCurrent(TKey key, Lazy<TValue> value)
    {
        lock (_syncRoot)
        {
            if (_entries.TryGetValue(key, out var entry) == false || ReferenceEquals(entry.Value, value) == false)
                return;
            _entries.Remove(key);
            _accessOrder.Remove(entry.Node);
        }
    }

    private void Touch(CacheEntry entry)
    {
        _accessOrder.Remove(entry.Node);
        _accessOrder.AddLast(entry.Node);
    }

    private void EvictLeastRecentlyUsed()
    {
        var node = _accessOrder.First;
        if (node == null)
            return;
        _accessOrder.RemoveFirst();
        _entries.Remove(node.Value);
        _evictionCount++;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(Lazy<TValue> value, LinkedListNode<TKey> node)
        {
            Value = value;
            Node = node;
        }

        public Lazy<TValue> Value { get; }

        public LinkedListNode<TKey> Node { get; }
    }
}