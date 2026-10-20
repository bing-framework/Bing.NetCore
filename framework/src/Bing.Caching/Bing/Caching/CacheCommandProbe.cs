namespace Bing.Caching;

/// <summary>
/// 指定性能探针识别的 Redis 调用类别。
/// </summary>
internal enum CacheCommandKind
{
    /// <summary>
    /// 未分类的调用。
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// 单键读取调用。
    /// </summary>
    SingleRead = 1,
    /// <summary>
    /// 批量读取调用。
    /// </summary>
    BatchRead = 2,
    /// <summary>
    /// 扫描调用。
    /// </summary>
    Scan = 3,
    /// <summary>
    /// 单键写入调用。
    /// </summary>
    SingleWrite = 4,
    /// <summary>
    /// 删除调用。
    /// </summary>
    Delete = 5
}

/// <summary>
/// 计量接入探针的逻辑缓存客户端调用。
/// </summary>
/// <remarks>该探针不推断网络请求数或客户端内部排队情况。</remarks>
internal sealed class CacheCommandProbe : IDisposable
{
    /// <summary>
    /// 当前异步执行流中的探针。
    /// </summary>
    private static readonly AsyncLocal<CacheCommandProbe> Current = new();
    /// <summary>
    /// 当前探针创建前的执行流探针。
    /// </summary>
    private readonly CacheCommandProbe _previous;
    /// <summary>
    /// 按调用类别累计的调用次数。
    /// </summary>
    private readonly long[] _kindCalls = new long[6];
    /// <summary>
    /// 按调用类别跟踪的当前并发数。
    /// </summary>
    private readonly int[] _kindActive = new int[6];
    /// <summary>
    /// 按调用类别跟踪的最大并发数。
    /// </summary>
    private readonly int[] _kindMaximum = new int[6];
    /// <summary>
    /// 当前在途调用数量。
    /// </summary>
    private int _active;
    /// <summary>
    /// 观测到的最大在途调用数量。
    /// </summary>
    private int _maximum;
    /// <summary>
    /// 累计调用次数。
    /// </summary>
    private long _calls;
    /// <summary>
    /// 显式报告的扫描页数。
    /// </summary>
    private long _scanPages;
    /// <summary>
    /// 扫描命令返回的物理键数量。
    /// </summary>
    private long _scanItems;
    /// <summary>
    /// 指示是否观测到显式扫描页边界。
    /// </summary>
    private int _scanPagesObserved;

    /// <summary>
    /// 初始化一个 <see cref="CacheCommandProbe"/> 类型的实例。
    /// </summary>
    /// <remarks>将新探针设为当前执行流的活动探针。</remarks>
    private CacheCommandProbe()
    {
        _previous = Current.Value;
        Current.Value = this;
    }

    /// <summary>
    /// 开始一段性能探针计量。
    /// </summary>
    /// <returns>当前执行流的性能探针。</returns>
    internal static CacheCommandProbe Begin() => new();
    /// <summary>
    /// 获取累计调用次数。
    /// </summary>
    internal long Calls => Interlocked.Read(ref _calls);
    /// <summary>
    /// 获取观测到的最大并发调用数。
    /// </summary>
    internal int Maximum => Volatile.Read(ref _maximum);
    /// <summary>
    /// 获取当前在途调用数。
    /// </summary>
    internal int Active => Volatile.Read(ref _active);

    /// <summary>
    /// 获取显式扫描命令或页流 Provider 报告的页数。
    /// </summary>
    /// <remarks>没有可见分页边界时返回 -1。</remarks>
    internal long ScanPages => Volatile.Read(ref _scanPagesObserved) == 0
        ? -1
        : Interlocked.Read(ref _scanPages);

    /// <summary>
    /// 获取扫描命令返回的物理键数量。
    /// </summary>
    /// <remarks>只统计已接入探针的扫描路径。</remarks>
    internal long ScanItems => Interlocked.Read(ref _scanItems);

    /// <summary>
    /// 记录一个显式扫描页及其返回项数量。
    /// </summary>
    /// <param name="itemCount">本页返回的物理键数量。</param>
    internal static void RecordScanPage(int itemCount)
    {
        if (itemCount < 0)
            throw new ArgumentOutOfRangeException(nameof(itemCount));
        var probe = Current.Value;
        probe?.RecordScanPageCore(itemCount);
    }

    /// <summary>
    /// 记录一个由 Provider 隐藏分页边界的扫描项。
    /// </summary>
    internal static void RecordScanItem()
    {
        var probe = Current.Value;
        if (probe != null)
            Interlocked.Increment(ref probe._scanItems);
    }

    /// <summary>
    /// 获取指定调用类别的最大并发数。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    /// <returns>该类别观测到的最大并发数。</returns>
    internal int GetMaximum(CacheCommandKind kind)
    {
        var index = GetKindIndex(kind);
        return Volatile.Read(ref _kindMaximum[index]);
    }

    /// <summary>
    /// 异步执行并跟踪缓存客户端命令。
    /// </summary>
    /// <typeparam name="T">命令结果类型。</typeparam>
    /// <param name="command">异步命令委托。</param>
    /// <returns>命令产生的异步结果。</returns>
    internal static Task<T> InvokeAsync<T>(Func<Task<T>> command)
        => InvokeAsync(CacheCommandKind.Unknown, command);

    /// <summary>
    /// 异步执行并跟踪缓存客户端命令。
    /// </summary>
    /// <param name="command">异步命令委托。</param>
    internal static Task InvokeAsync(Func<Task> command)
        => InvokeAsync(CacheCommandKind.Unknown, command);

    /// <summary>
    /// 异步执行并跟踪缓存客户端命令。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    /// <param name="command">异步命令委托。</param>
    internal static Task InvokeAsync(CacheCommandKind kind, Func<Task> command)
    {
        var probe = Current.Value;
        return probe == null ? command() : probe.TrackAsync(kind, command);
    }

    /// <summary>
    /// 异步执行并跟踪缓存客户端命令。
    /// </summary>
    /// <typeparam name="T">命令结果类型。</typeparam>
    /// <param name="kind">调用类别。</param>
    /// <param name="command">异步命令委托。</param>
    /// <returns>命令产生的异步结果。</returns>
    internal static Task<T> InvokeAsync<T>(CacheCommandKind kind, Func<Task<T>> command)
    {
        var probe = Current.Value;
        return probe == null ? command() : probe.TrackAsync(kind, command);
    }

    /// <summary>
    /// 获取指定调用类别的调用次数。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    /// <returns>该类别累计的调用次数。</returns>
    internal long GetCalls(CacheCommandKind kind)
    {
        var index = (int)kind;
        if (index < 0 || index >= _kindCalls.Length)
            throw new ArgumentOutOfRangeException(nameof(kind));
        return Interlocked.Read(ref _kindCalls[index]);
    }

    /// <summary>
    /// 获取有调用记录的类别及其调用次数。
    /// </summary>
    /// <returns>按类别名称索引的调用次数。</returns>
    internal IReadOnlyDictionary<string, long> GetBreakdown()
    {
        var result = new Dictionary<string, long>(6, StringComparer.Ordinal);
        for (var index = 0; index < _kindCalls.Length; index++)
        {
            var count = Interlocked.Read(ref _kindCalls[index]);
            if (count > 0)
                result[((CacheCommandKind)index).ToString()] = count;
        }
        return result;
    }

    /// <summary>
    /// 获取有并发记录的类别及其最大并发数。
    /// </summary>
    /// <returns>按类别名称索引的最大并发数。</returns>
    internal IReadOnlyDictionary<string, int> GetMaximumBreakdown()
    {
        var result = new Dictionary<string, int>(6, StringComparer.Ordinal);
        for (var index = 0; index < _kindMaximum.Length; index++)
        {
            var maximum = Volatile.Read(ref _kindMaximum[index]);
            if (maximum > 0)
                result[((CacheCommandKind)index).ToString()] = maximum;
        }
        return result;
    }

    /// <summary>
    /// 跟踪异步命令执行。
    /// </summary>
    /// <typeparam name="T">命令结果类型。</typeparam>
    /// <param name="kind">调用类别。</param>
    /// <param name="command">异步命令委托。</param>
    /// <returns>命令产生的异步结果。</returns>
    private async Task<T> TrackAsync<T>(CacheCommandKind kind, Func<Task<T>> command)
    {
        Enter(kind);
        try { return await command().ConfigureAwait(false); }
        finally { Exit(kind); }
    }

    /// <summary>
    /// 跟踪异步命令执行。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    /// <param name="command">异步命令委托。</param>
    private async Task TrackAsync(CacheCommandKind kind, Func<Task> command)
    {
        Enter(kind);
        try { await command().ConfigureAwait(false); }
        finally { Exit(kind); }
    }

    /// <summary>
    /// 记录指定类别命令开始执行。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    private void Enter(CacheCommandKind kind)
    {
        var index = GetKindIndex(kind);
        Interlocked.Increment(ref _calls);
        Interlocked.Increment(ref _kindCalls[index]);

        var active = Interlocked.Increment(ref _active);
        UpdateMaximum(ref _maximum, active);

        var kindActive = Interlocked.Increment(ref _kindActive[index]);
        UpdateMaximum(ref _kindMaximum[index], kindActive);
    }

    /// <summary>
    /// 记录指定类别命令结束执行。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    private void Exit(CacheCommandKind kind)
    {
        var index = GetKindIndex(kind);
        Interlocked.Decrement(ref _kindActive[index]);
        Interlocked.Decrement(ref _active);
    }

    /// <summary>
    /// 累计一个扫描页及其返回项数量。
    /// </summary>
    /// <param name="itemCount">本页返回的物理键数量。</param>
    private void RecordScanPageCore(int itemCount)
    {
        Interlocked.Increment(ref _scanPages);
        Interlocked.Add(ref _scanItems, itemCount);
        Volatile.Write(ref _scanPagesObserved, 1);
    }

    /// <summary>
    /// 获取调用类别对应的计数索引。
    /// </summary>
    /// <param name="kind">调用类别。</param>
    /// <returns>计数数组索引。</returns>
    private static int GetKindIndex(CacheCommandKind kind)
    {
        var index = (int)kind;
        if (index < 0 || index >= 6)
            throw new ArgumentOutOfRangeException(nameof(kind));
        return index;
    }

    /// <summary>
    /// 将计数更新为更大的观测值。
    /// </summary>
    /// <param name="target">待更新的计数。</param>
    /// <param name="value">本次观测值。</param>
    private static void UpdateMaximum(ref int target, int value)
    {
        var maximum = Volatile.Read(ref target);
        while (value > maximum)
        {
            var observed = Interlocked.CompareExchange(ref target, value, maximum);
            if (observed == maximum)
                return;
            maximum = observed;
        }
    }

    /// <inheritdoc />
    public void Dispose() => Current.Value = _previous;
}
