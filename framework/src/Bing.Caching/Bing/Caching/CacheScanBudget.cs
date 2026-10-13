namespace Bing.Caching;

/// <summary>
/// 对一次扫描操作的已验证候选键数量进行有界计数。
/// </summary>
internal sealed class CacheScanBudget
{
    /// <summary>
    /// 保护扫描预算状态的同步对象。
    /// </summary>
    private readonly object _gate = new object();
    /// <summary>
    /// 当前异步执行流所关联的分片索引。
    /// </summary>
    private readonly AsyncLocal<int?> _currentShard = new AsyncLocal<int?>();
    /// <summary>
    /// 已完整完成的分片索引集合。
    /// </summary>
    private readonly HashSet<int> _completedShards = new HashSet<int>();
    /// <summary>
    /// 候选键数量上限。
    /// </summary>
    private readonly int _maximum;
    /// <summary>
    /// 扫描页数上限。
    /// </summary>
    private readonly int _maximumPages;
    /// <summary>
    /// 已接受的候选键数量。
    /// </summary>
    private int _count;
    /// <summary>
    /// 已返回的扫描页数量。
    /// </summary>
    private int _pageCount;
    /// <summary>
    /// 首个已知失败的分片索引。
    /// </summary>
    private int? _failedShard;

    /// <summary>
    /// 初始化一个 <see cref="CacheScanBudget"/> 类型的实例。
    /// </summary>
    /// <param name="maximum">候选键数量上限。</param>
    internal CacheScanBudget(int maximum)
        : this(maximum, 0)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheScanBudget"/> 类型的实例。
    /// </summary>
    /// <param name="resources">缓存资源配置。</param>
    internal CacheScanBudget(CacheResourceOptions resources)
        : this(resources?.MaxScanKeys ?? throw new ArgumentNullException(nameof(resources)),
            resources.MaxScanPages)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheScanBudget"/> 类型的实例。
    /// </summary>
    /// <param name="maximum">候选键数量上限。</param>
    /// <param name="maximumPages">扫描页数上限。</param>
    internal CacheScanBudget(int maximum, int maximumPages)
    {
        if (maximum < 0)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        if (maximumPages < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        _maximum = maximum;
        _maximumPages = maximumPages;
    }

    /// <summary>
    /// 获取已接受的候选键数量。
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_gate)
                return _count;
        }
    }

    /// <summary>
    /// 获取已返回的扫描页数量。
    /// </summary>
    internal int PageCount
    {
        get
        {
            lock (_gate)
                return _pageCount;
        }
    }

    /// <summary>
    /// 设置当前分片扫描进度。
    /// </summary>
    /// <param name="completedShardCount">已完成分片的数量。</param>
    /// <param name="failedShard">当前失败的分片索引。</param>
    /// <remarks>此方法用于顺序分片扫描；非分片调用方无需设置。</remarks>
    internal void SetShardProgress(int completedShardCount, int failedShard)
    {
        if (completedShardCount < 0)
            throw new ArgumentOutOfRangeException(nameof(completedShardCount));
        if (failedShard < 0)
            throw new ArgumentOutOfRangeException(nameof(failedShard));
        lock (_gate)
        {
            _completedShards.Clear();
            for (var shard = 0; shard < completedShardCount; shard++)
                _completedShards.Add(shard);
            _failedShard = failedShard;
        }
    }

    /// <summary>
    /// 将当前异步执行流绑定到指定分片。
    /// </summary>
    /// <param name="shard">分片索引。</param>
    /// <returns>恢复此前分片上下文的作用域。</returns>
    internal IDisposable EnterShard(int shard)
    {
        if (shard < 0)
            throw new ArgumentOutOfRangeException(nameof(shard));
        var previous = _currentShard.Value;
        _currentShard.Value = shard;
        return new ShardScope(_currentShard, previous);
    }

    /// <summary>
    /// 标记分片已完成。
    /// </summary>
    /// <param name="shard">已完成的分片索引。</param>
    /// <remarks>并发扫描时，已完成的分片索引可能不连续。</remarks>
    internal void MarkShardCompleted(int shard)
    {
        if (shard < 0)
            throw new ArgumentOutOfRangeException(nameof(shard));
        lock (_gate)
            _completedShards.Add(shard);
    }

    /// <summary>
    /// 标记发生底层失败的分片。
    /// </summary>
    /// <param name="shard">失败的分片索引。</param>
    internal void MarkShardFailed(int shard)
    {
        if (shard < 0)
            throw new ArgumentOutOfRangeException(nameof(shard));
        lock (_gate)
        {
            if (!_failedShard.HasValue)
                _failedShard = shard;
        }
    }

    /// <summary>
    /// 记录一个通过校验的候选键。
    /// </summary>
    /// <remarks>候选键需先通过命名空间和物理键校验。</remarks>
    internal void Consume()
    {
        lock (_gate)
        {
            if (_maximum > 0 && _count >= _maximum)
                throw CreateLimitException(_maximum, _count, CacheScanLimitKind.CandidateKeys);
            _count++;
        }
    }

    /// <summary>
    /// 记录一个已返回的扫描结果页。
    /// </summary>
    /// <remarks>达到页数上限后会阻止继续发出扫描命令。</remarks>
    internal void ConsumePage()
    {
        lock (_gate)
        {
            if (_maximumPages > 0 && _pageCount >= _maximumPages)
                throw CreateLimitException(_maximumPages, _pageCount, CacheScanLimitKind.ScanPages);
            _pageCount++;
        }
    }

    /// <summary>
    /// 创建描述当前扫描进度的资源上限异常。
    /// </summary>
    /// <param name="limit">资源数量上限。</param>
    /// <param name="consumed">触发上限前已消耗的资源数量。</param>
    /// <param name="kind">资源计量类型。</param>
    /// <returns>包含分片进度信息的资源上限异常。</returns>
    private CacheScanLimitExceededException CreateLimitException(int limit, int consumed,
        CacheScanLimitKind kind)
    {
        var completed = new int[_completedShards.Count];
        _completedShards.CopyTo(completed);
        Array.Sort(completed);
        var failed = _currentShard.Value ?? _failedShard;
        return new CacheScanLimitExceededException(limit, consumed, completed, failed, kind);
    }

    /// <summary>
    /// 恢复当前执行流分片上下文的作用域。
    /// </summary>
    private sealed class ShardScope : IDisposable
    {
        /// <summary>
        /// 当前执行流中的分片索引槽位。
        /// </summary>
        private readonly AsyncLocal<int?> _slot;
        /// <summary>
        /// 进入作用域前的分片索引。
        /// </summary>
        private readonly int? _previous;
        /// <summary>
        /// 指示分片作用域是否已释放；为 <see langword="true"/> 时跳过重复恢复。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个 <see cref="ShardScope"/> 类型的实例。
        /// </summary>
        /// <param name="slot">当前执行流中的分片索引槽位。</param>
        /// <param name="previous">进入作用域前的分片索引。</param>
        internal ShardScope(AsyncLocal<int?> slot, int? previous)
        {
            _slot = slot;
            _previous = previous;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _slot.Value = _previous;
        }
    }
}
