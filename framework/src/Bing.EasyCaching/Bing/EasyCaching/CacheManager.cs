using Bing.Caching;
using Bing.Extensions;
using StackExchange.Redis;
using System.Runtime.CompilerServices;

namespace Bing.EasyCaching;

/// <summary>
/// EasyCaching 缓存适配器。
/// </summary>
public partial class CacheManager : ICache
{
    #region 字段

    /// <summary>
    /// 执行缓存读写的底层 Provider。
    /// </summary>
    private readonly IEasyCachingProviderBase _provider;
    /// <summary>
    /// 提供 Redis 键扫描和批量操作能力的 Provider。
    /// </summary>
    private readonly IEasyCachingProvider _cachingProvider;
    /// <summary>
    /// Hybrid 缓存的本地层提供器。
    /// </summary>
    private readonly IEasyCachingProvider _localProvider;
    /// <summary>
    /// 当前缓存实例的命名空间和资源配置。
    /// </summary>
    private readonly CacheConfiguration _configuration;
    /// <summary>
    /// 按前缀枚举逻辑键的可选异步数据源。
    /// </summary>
    private readonly Func<string, CancellationToken, IAsyncEnumerable<string>> _keyStream;
    /// <summary>
    /// 当前 Provider 对应的分布式回源租约协调器。
    /// </summary>
    private RedisLeaseCoordinator _coordinator;
    /// <summary>
    /// 监视 Hybrid 失效总线状态的跟踪器。
    /// </summary>
    private HybridBusFailureTracker _busFailureTracker;
    /// <summary>
    /// 标记本地层失效与回填操作之间的代次变化。
    /// </summary>
    private readonly HybridRefillGeneration _hybridRefillGeneration = new();
    // 同一个底层 Provider 的多个适配器实例仍共享进程内同键合并；不同 Provider
    // 必须隔离，避免同名键在不同 Redis 端点之间互相等待或误判递归。
    /// <summary>
    /// 保存同一 Provider 共享的进程内回源协调状态。
    /// </summary>
    private sealed class CoordinationState
    {
        /// <summary>
        /// 保护本地门闩字典的同步对象。
        /// </summary>
        internal readonly object GateSync = new();
        /// <summary>
        /// 按物理键保存的回源门闩。
        /// </summary>
        internal readonly Dictionary<string, LocalGate> Gates = new(StringComparer.Ordinal);
        /// <summary>
        /// 记录当前执行流中的回源键以检测递归调用。
        /// </summary>
        internal readonly AsyncLocal<HashSet<string>> ActiveFactories = new();
    }

    /// <summary>
    /// 按 Provider 实例共享的进程内回源协调状态表。
    /// </summary>
    private static readonly ConditionalWeakTable<IEasyCachingProvider, CoordinationState> CoordinationStates = new();
    /// <summary>
    /// 当前缓存管理器使用的 Provider 协调状态。
    /// </summary>
    private readonly CoordinationState _coordination;

    /// <summary>
    /// 表示一个物理键的进程内回源门闩及其引用计数。
    /// </summary>
    private sealed class LocalGate
    {
        /// <summary>
        /// 串行化同一物理键的回源操作。
        /// </summary>
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
        /// <summary>
        /// 当前引用该门闩的调用数。
        /// </summary>
        internal int Users;
    }

    /// <summary>
    /// 获取指定键的本地回源门闩并增加引用数。
    /// </summary>
    /// <param name="key">物理缓存键。</param>
    /// <returns>对应的本地回源门闩。</returns>
    private LocalGate AddGate(string key)
    {
        lock (_coordination.GateSync)
        {
            if (!_coordination.Gates.TryGetValue(key, out var gate))
                _coordination.Gates[key] = gate = new LocalGate();
            gate.Users++;
            return gate;
        }
    }

    /// <summary>
    /// 释放指定键的本地回源门闩引用。
    /// </summary>
    /// <param name="key">物理缓存键。</param>
    /// <param name="gate">待释放的门闩。</param>
    /// <param name="entered">调用方是否已进入门闩。</param>
    private void ReleaseGate(string key, LocalGate gate, bool entered)
    {
        if (entered) gate.Semaphore.Release();
        lock (_coordination.GateSync)
        {
            if (--gate.Users == 0)
            {
                _coordination.Gates.Remove(key);
                gate.Semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// 检查当前执行流是否递归请求同一个键。
    /// </summary>
    /// <param name="key">物理缓存键。</param>
    private void CheckRecursion(string key)
    {
        if (_coordination.ActiveFactories.Value?.Contains(key) == true)
            throw new InvalidOperationException("缓存回源不能递归请求同一个键。");
    }

    /// <summary>
    /// 在当前执行流上下文中调用异步回源委托。
    /// </summary>
    /// <typeparam name="T">回源结果类型。</typeparam>
    /// <param name="key">正在回源的物理缓存键。</param>
    /// <param name="action">接收取消令牌的回源委托。</param>
    /// <param name="cancellationToken">回源取消令牌。</param>
    /// <returns>回源委托产生的结果。</returns>
    private async Task<T> InvokeFactory<T>(string key, Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var previous = _coordination.ActiveFactories.Value;
        var active = previous == null ? new HashSet<string>() : new HashSet<string>(previous);
        active.Add(key);
        _coordination.ActiveFactories.Value = active;
        try { return await action(cancellationToken); }
        finally { _coordination.ActiveFactories.Value = previous; }
    }

    #endregion

    #region 构造方法

    /// <summary>
    /// 初始化一个 <see cref="CacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="provider">用于分布式读写和扫描的 EasyCaching Provider。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="hybridProvider">可选的 Hybrid 缓存 Provider。</param>
    /// <param name="localProvider">Hybrid 缓存的本地层 Provider。</param>
    /// <remarks>客户端分片拓扑不受此适配器支持；Redis Cluster 需要 Provider 暴露 Redis 数据库。</remarks>
    public CacheManager(IEasyCachingProvider provider, CacheConfiguration configuration,
        IHybridCachingProvider hybridProvider = null, IEasyCachingProvider localProvider = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (_configuration.Topology == CacheTopology.ClientSharded)
            throw new NotSupportedException("EasyCaching 适配器尚不支持客户端分片拓扑。");
        if (_configuration.Topology == CacheTopology.RedisCluster && provider?.Database is not IDatabase)
            throw new NotSupportedException("Redis Cluster 需要 EasyCaching Redis Provider 提供 StackExchange.Redis 数据库。");
        _localProvider = localProvider;
        if (provider != null)
        {
            _provider = provider;
            _cachingProvider = provider;
        }
        if (hybridProvider != null)
            _provider = hybridProvider;
        _provider.CheckNull(nameof(provider));
        _coordination = CoordinationStates.GetValue(_cachingProvider,
            _ => new CoordinationState());
    }

    /// <summary>
    /// 初始化一个 <see cref="CacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="provider">EasyCaching Provider。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="keyStream">按前缀枚举逻辑键的数据源。</param>
    /// <remarks>此重载供内部适配器提供自定义键扫描源。</remarks>
    internal CacheManager(IEasyCachingProvider provider, CacheConfiguration configuration,
        Func<string, CancellationToken, IAsyncEnumerable<string>> keyStream)
        : this(provider, configuration)
    {
        _keyStream = keyStream;
    }

    /// <summary>
    /// 设置分布式回源租约协调器。
    /// </summary>
    /// <param name="coordinator">回源租约协调器。</param>
    internal void SetCoordinator(RedisLeaseCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    /// <summary>
    /// 设置 Hybrid 总线失败跟踪器并注册本地失效观察器。
    /// </summary>
    /// <param name="tracker">总线失败跟踪器。</param>
    internal void SetBusFailureTracker(HybridBusFailureTracker tracker)
    {
        _busFailureTracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        if (_localProvider != null)
            tracker.SetInvalidationObserver(MarkHybridMutation);
    }

    /// <summary>
    /// 标记 Hybrid 本地缓存发生失效或变更。
    /// </summary>
    internal void MarkHybridMutation()
    {
        if (_localProvider != null)
            _hybridRefillGeneration.Advance();
    }

    /// <summary>
    /// 获取当前 Hybrid 回填代次。
    /// </summary>
    /// <returns>当前代次值。</returns>
    private long CaptureHybridGeneration() => _hybridRefillGeneration.Capture();

    #endregion

    #region Exists

    /// <inheritdoc />
    public bool Exists(CacheKey key)
    {
        key.Validate();
        return Exists(key.Key);
    }

    /// <inheritdoc />
    public bool Exists(string key)
    {
        return _provider.Exists(PhysicalKey(key));
    }

    #endregion

    #region ExistsAsync

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await ExistsAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _provider.ExistsAsync(PhysicalKey(key), cancellationToken);
    }

    #endregion

    #region Get

    /// <inheritdoc />
    public T Get<T>(CacheKey key)
    {
        key.Validate();
        return Get<T>(key.Key);
    }

    /// <inheritdoc />
    public T Get<T>(string key)
    {
        var result = _provider.Get<T>(PhysicalKey(key));
        return result.Value;
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys)
    {
        return GetAll<T>(ToKeys(keys));
    }

    /// <summary>
    /// 将类型化缓存键转换为逻辑键序列。
    /// </summary>
    /// <param name="keys">类型化缓存键集合。</param>
    /// <returns>对应的逻辑键序列。</returns>
    private IEnumerable<string> ToKeys(IEnumerable<CacheKey> keys)
    {
        keys.CheckNull(nameof(keys));
        var cacheKeys = keys.ToList();
        cacheKeys.ForEach(t => t.Validate());
        return cacheKeys.Select(t => t.Key);
    }

    /// <summary>
    /// 延迟将类型化缓存键转换为逻辑键序列。
    /// </summary>
    /// <param name="keys">类型化缓存键集合。</param>
    /// <returns>按需验证和转换的逻辑键序列。</returns>
    private static IEnumerable<string> ToKeysLazily(IEnumerable<CacheKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        return Enumerate();

        IEnumerable<string> Enumerate()
        {
            foreach (var key in keys)
            {
                key.Validate();
                yield return key.Key;
            }
        }
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var result = new List<T>();
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            batch.Add(PhysicalKey(key));
            if (batch.Count < _configuration.BatchSize)
                continue;
            AppendBatch(result, batch);
            batch.Clear();
        }
        if (batch.Count > 0)
            AppendBatch(result, batch);
        return result;
    }

    /// <summary>
    /// 读取一批物理键并按输入顺序追加缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="result">接收缓存值的集合。</param>
    /// <param name="physicalKeys">待读取的物理键。</param>
    private void AppendBatch<T>(ICollection<T> result, IList<string> physicalKeys)
    {
        Validate();
        var values = GetAllPhysicalValues<T>(physicalKeys);
        if (values == null)
        {
            foreach (var physicalKey in physicalKeys)
            {
                var value = _provider.Get<T>(physicalKey);
                result.Add(value.HasValue ? value.Value : default);
            }
            return;
        }

        foreach (var physicalKey in physicalKeys)
            result.Add(values.TryGetValue(physicalKey, out var value) && value.HasValue ? value.Value : default);
    }

    /// <summary>
    /// 验证当前 Provider 支持键扫描操作。
    /// </summary>
    private void Validate()
    {
        if (_cachingProvider == null)
            throw new NotSupportedException("当前 EasyCaching 实例未配置支持键枚举的 Provider。");
    }

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> action, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, action, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> action, CacheOptions options = null)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        var physical = PhysicalKey(key);
        CheckRecursion(physical);
        var cached = _provider.Get<T>(physical);
        if (cached.HasValue) return cached.Value;
        var gate = AddGate(physical);
        var entered = false;
        try
        {
            entered = gate.Semaphore.Wait(_configuration.Coordination.WaitTimeout);
            if (!entered) throw new TimeoutException("等待本地缓存回源超时。");
            cached = _provider.Get<T>(physical);
            if (cached.HasValue) return cached.Value;
            using var admission = CacheFactoryAdmission.Enter(_configuration.Resources, _configuration.Coordination.WaitTimeout);
            var previous = _coordination.ActiveFactories.Value;
            var active = previous == null ? new HashSet<string>() : new HashSet<string>(previous);
            active.Add(physical);
            _coordination.ActiveFactories.Value = active;
            try
            {
                if (_coordinator != null)
                {
                    var result = _coordinator.GetOrCreate(physical, action, GetExpiration(options));
                    if (_localProvider != null) ReadHybridBatch<T>(new[] { physical });
                    return result;
                }
                using var timeout = new CancellationTokenSource(_configuration.Coordination.RetrievalTimeout);
                var value = action();
                CacheFactoryExecution.ThrowIfCancelled(timeout.Token, CancellationToken.None);
                ExecuteWithBusCheck(() => _provider.Set(physical, value, GetExpiration(options)));
                return value;
            }
            finally { _coordination.ActiveFactories.Value = previous; }
        }
        finally { ReleaseGate(physical, gate, entered); }
    }

    /// <summary>
    /// 获取本次写入采用的缓存有效期。
    /// </summary>
    /// <param name="options">缓存选项。</param>
    /// <returns>规范化后的缓存有效期。</returns>
    private TimeSpan GetExpiration(CacheOptions options)
    {
        return _configuration.GetExpiration(options);
    }

    #endregion

    #region GetAsync

    /// <inheritdoc />
    public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        return await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
            () => _provider.GetAsync(PhysicalKey(key), type, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync<T>(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var result = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
            () => _provider.GetAsync<T>(PhysicalKey(key), cancellationToken));
        return result.Value;
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default)
    {
        return await GetAllAsync<T>(ToKeysLazily(keys), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var result = CacheCollectionCount.TryGetCount(keys, out var count)
            ? new List<T>(count)
            : new List<T>();
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("缓存键不能为空。", nameof(keys));
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;

            await AppendBatchAsync(result, batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }

        if (batch.Count > 0)
            await AppendBatchAsync(result, batch, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 异步读取一批逻辑键并按输入顺序追加缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="result">接收缓存值的集合。</param>
    /// <param name="logicalKeys">逻辑键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task AppendBatchAsync<T>(ICollection<T> result, IList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        Validate();
        var physicalKeys = CacheKeyFormat.GetPhysicalKeys(_configuration, logicalKeys);
        var distinctPhysicalKeys = new List<string>(physicalKeys.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < physicalKeys.Length; index++)
            if (seen.Add(physicalKeys[index]))
                distinctPhysicalKeys.Add(physicalKeys[index]);

        var values = await GetAllPhysicalValuesAsync<T>(distinctPhysicalKeys, cancellationToken)
            .ConfigureAwait(false);
        if (values == null)
        {
            for (var index = 0; index < physicalKeys.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                    () => _provider.GetAsync<T>(physicalKeys[index], cancellationToken))
                    .ConfigureAwait(false);
                result.Add(value.HasValue ? value.Value : default);
            }
            return;
        }

        for (var index = 0; index < physicalKeys.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var physicalKey = physicalKeys[index];
            result.Add(values.TryGetValue(physicalKey, out var value) && value.HasValue
                ? value.Value
                : default);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<CacheKey> keys,
        CancellationToken cancellationToken = default) => GetAllStreamAsync<T>(ToKeysLazily(keys), cancellationToken);

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<string> keys,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("缓存键不能为空。", nameof(keys));
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;

            await foreach (var entry in ReadAllBatchAsync<T>(batch, cancellationToken))
                yield return entry;
            batch.Clear();
        }

        if (batch.Count == 0)
            yield break;
        await foreach (var entry in ReadAllBatchAsync<T>(batch, cancellationToken))
            yield return entry;
    }

    /// <summary>
    /// 流式读取一批逻辑键并保留命中状态。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">逻辑键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入顺序一致的缓存条目序列。</returns>
    private async IAsyncEnumerable<CacheEntry<T>> ReadAllBatchAsync<T>(IList<string> logicalKeys,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Validate();
        var physicalKeys = CacheKeyFormat.GetPhysicalKeys(_configuration, logicalKeys);
        var distinctPhysicalKeys = new List<string>(physicalKeys.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < physicalKeys.Length; index++)
            if (seen.Add(physicalKeys[index]))
                distinctPhysicalKeys.Add(physicalKeys[index]);
        var values = await GetAllPhysicalValuesAsync<T>(distinctPhysicalKeys, cancellationToken);
        if (values == null)
        {
            for (var index = 0; index < logicalKeys.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                    () => _provider.GetAsync<T>(physicalKeys[index], cancellationToken));
                yield return new CacheEntry<T>(logicalKeys[index], value.HasValue ? value.Value : default,
                    value.HasValue);
            }
            yield break;
        }

        for (var index = 0; index < logicalKeys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var physicalKey = physicalKeys[index];
            if (values.TryGetValue(physicalKey, out var value) && value.HasValue)
                yield return new CacheEntry<T>(logicalKeys[index], value.Value, true);
            else
                yield return new CacheEntry<T>(logicalKeys[index], default, false);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> action, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        return await GetAsync(key, _ => action(), options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> action, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        return await GetAsync(key, _ => action(), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> action,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return GetAsync(key.Key, action, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> action,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        cancellationToken.ThrowIfCancellationRequested();
        var physical = PhysicalKey(key);
        CheckRecursion(physical);
        var cached = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
            () => _provider.GetAsync<T>(physical, cancellationToken));
        if (cached.HasValue) return cached.Value;
        var gate = AddGate(physical);
        var entered = false;
        try
        {
            entered = await gate.Semaphore.WaitAsync(_configuration.Coordination.WaitTimeout, cancellationToken);
            if (!entered) throw new TimeoutException("等待本地缓存回源超时。");
            cached = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => _provider.GetAsync<T>(physical, cancellationToken));
            if (cached.HasValue) return cached.Value;
            using var admission = await CacheFactoryAdmission.EnterAsync(_configuration.Resources, _configuration.Coordination.WaitTimeout, cancellationToken);
            if (_coordinator != null)
            {
                var result = await _coordinator.GetOrCreateAsync(physical,
                    token => InvokeFactory(physical, action, token), GetExpiration(options), cancellationToken);
                if (_localProvider != null)
                    await ReadHybridBatchAsync<T>(new[] { physical }, cancellationToken);
                return result;
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_configuration.Coordination.RetrievalTimeout);
            var value = await CacheFactoryExecution.InvokeAsync(token => InvokeFactory(physical, action, token),
                timeout.Token, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteWithBusCheckAsync(() => CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
                () => _provider.SetAsync(physical, value, GetExpiration(options), cancellationToken)));
            return value;
        }
        finally { ReleaseGate(physical, gate, entered); }
    }

    #endregion

    #region GetByPrefix

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
    {
        return GetByPrefixWithBudget<T>(prefix,
            new CacheScanBudget(_configuration.Resources));
    }

    /// <summary>
    /// 按前缀读取缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    /// <returns>前缀匹配且命中的缓存值。</returns>
    internal List<T> GetByPrefixWithBudget<T>(string prefix, CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (prefix.IsEmpty())
            return new List<T>();
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        var result = new List<T>();
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in FindKeys(key => key.StartsWith(prefix, StringComparison.Ordinal),
                     CacheKeyFormat.GetPrefixPattern(_configuration, prefix), scanBudget,
                     CacheKeyFormat.GetEncodedLogicalPrefix(prefix)))
        {
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            AppendHitValues(result, batch);
            batch.Clear();
        }
        if (batch.Count > 0)
            AppendHitValues(result, batch);
        return result;
    }

    /// <summary>
    /// 读取一批物理键并仅追加已命中的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="result">接收缓存值的集合。</param>
    /// <param name="physicalKeys">待读取的物理键。</param>
    private void AppendHitValues<T>(ICollection<T> result, IList<string> physicalKeys)
    {
        Validate();
        var values = GetAllPhysicalValues<T>(physicalKeys);
        if (values == null)
        {
            foreach (var physicalKey in physicalKeys)
            {
                var value = _provider.Get<T>(physicalKey);
                if (value.HasValue)
                    result.Add(value.Value);
            }
            return;
        }

        foreach (var physicalKey in physicalKeys)
            if (values.TryGetValue(physicalKey, out var value) && value.HasValue)
                result.Add(value.Value);
    }

    #endregion

    #region GetByPrefixAsync

    /// <inheritdoc />
    public Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default) =>
        GetByPrefixWithBudgetAsync<T>(prefix, cancellationToken,
            new CacheScanBudget(_configuration.Resources));

    /// <summary>
    /// 异步按前缀读取缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    /// <returns>前缀匹配且命中的缓存值。</returns>
    internal async Task<List<T>> GetByPrefixWithBudgetAsync<T>(string prefix,
        CancellationToken cancellationToken, CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        var result = new List<T>();
        if (prefix.IsEmpty())
            return result;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        var batch = new List<string>(_configuration.BatchSize);
        await foreach (var physicalKey in EnumeratePhysicalPrefixKeysAsync(prefix, cancellationToken,
                           scanBudget).ConfigureAwait(false))
        {
            batch.Add(physicalKey);
            if (batch.Count < _configuration.BatchSize)
                continue;

            await AppendHitValuesAsync(result, batch, cancellationToken).ConfigureAwait(false);
            batch.Clear();
        }

        if (batch.Count > 0)
            await AppendHitValuesAsync(result, batch, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 扫描并流式枚举前缀匹配的物理键。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    /// <returns>匹配前缀的物理键序列。</returns>
    private async IAsyncEnumerable<string> EnumeratePhysicalPrefixKeysAsync(string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken,
        CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (prefix.IsEmpty())
            yield break;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        Validate();

        var namespacePattern = CacheKeyFormat.GetNamespacePattern(_configuration);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        if (_cachingProvider.Database is IDatabase database)
        {
            var scanTopology = await ValidateClusterScanTopologyAsync(database, cancellationToken);
            foreach (var server in scanTopology.Servers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!server.IsConnected)
                    throw new InvalidOperationException("缓存扫描主节点不可用。");
                 await foreach (var redisKey in EnumerateRedisKeysAsync(server, database.Database, scanPattern,
                                    scanBudget, cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var physicalKey = redisKey.ToString();
                    if (!CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physicalKey, prefix, encodedPrefix))
                        continue;
                    scanBudget.Consume();
                    yield return physicalKey;
                }
            }

            await EnsureClusterScanTopologyUnchangedAsync(database, scanTopology.SlotOwnership,
                cancellationToken).ConfigureAwait(false);
            yield break;
        }

        var namespacePrefix = namespacePattern.Substring(0, namespacePattern.Length - 1);
        await foreach (var physicalKey in EnumerateProviderKeysAsync(namespacePrefix, cancellationToken, scanBudget)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physicalKey, prefix, encodedPrefix))
                continue;
            scanBudget.Consume();
            yield return physicalKey;
        }
    }

    /// <summary>
    /// 异步读取一批物理键并仅追加已命中的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="result">接收缓存值的集合。</param>
    /// <param name="physicalKeys">待读取的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task AppendHitValuesAsync<T>(ICollection<T> result, IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        Validate();
        var values = await GetAllPhysicalValuesAsync<T>(physicalKeys, cancellationToken)
            .ConfigureAwait(false);
        if (values == null)
        {
            foreach (var physicalKey in physicalKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                    () => _provider.GetAsync<T>(physicalKey, cancellationToken)).ConfigureAwait(false);
                if (value.HasValue)
                    result.Add(value.Value);
            }
            return;
        }

        foreach (var physicalKey in physicalKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (values.TryGetValue(physicalKey, out var value) && value.HasValue)
                result.Add(value.Value);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamAsync<T>(string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entry in GetByPrefixStreamWithBudgetAsync<T>(prefix, cancellationToken,
                           new CacheScanBudget(_configuration.Resources)))
            yield return entry;
    }

    /// <summary>
    /// 获取使用指定扫描预算的前缀缓存流。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    /// <returns>前缀匹配缓存条目的异步序列。</returns>
    internal IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamWithBudgetAsync<T>(string prefix,
        CancellationToken cancellationToken, CacheScanBudget scanBudget)
    {
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        return GetByPrefixStreamCoreAsync<T>(prefix, cancellationToken, scanBudget);
    }

    /// <summary>
    /// 扫描并流式读取指定前缀下的缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    /// <returns>前缀匹配缓存条目的异步序列。</returns>
    private async IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamCoreAsync<T>(string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken,
        CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (prefix.IsEmpty())
            yield break;
        Validate();

        var batchPhysicalKeys = new List<string>(_configuration.BatchSize);
        var batchLogicalKeys = new List<string>(_configuration.BatchSize);
        var namespacePattern = CacheKeyFormat.GetNamespacePattern(_configuration);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        if (_cachingProvider.Database is IDatabase database)
        {
            var scanTopology = await ValidateClusterScanTopologyAsync(database, cancellationToken);
            var scanServers = scanTopology.Servers;
            foreach (var server in scanServers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!server.IsConnected)
                    throw new InvalidOperationException("缓存扫描主节点不可用。");
                await foreach (var redisKey in EnumerateRedisKeysAsync(server, database.Database, scanPattern,
                                   scanBudget, cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var physicalKey = redisKey.ToString();
                    if (!CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physicalKey, encodedPrefix,
                            out var logicalKey))
                        continue;
                    scanBudget.Consume();
                    batchPhysicalKeys.Add(physicalKey);
                    batchLogicalKeys.Add(logicalKey);
                    if (batchPhysicalKeys.Count < _configuration.BatchSize)
                        continue;
                    await foreach (var entry in ReadPhysicalBatchAsync<T>(batchPhysicalKeys, batchLogicalKeys, cancellationToken))
                        yield return entry;
                    batchPhysicalKeys.Clear();
                    batchLogicalKeys.Clear();
                }
            }
            await EnsureClusterScanTopologyUnchangedAsync(database, scanTopology.SlotOwnership,
                cancellationToken);
        }
        else
        {
            var namespacePrefix = namespacePattern.Substring(0, namespacePattern.Length - 1);
            await foreach (var physicalKey in EnumerateProviderKeysAsync(namespacePrefix, cancellationToken, scanBudget)
                               .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physicalKey, encodedPrefix,
                        out var logicalKey))
                    continue;
                scanBudget.Consume();
                batchPhysicalKeys.Add(physicalKey);
                batchLogicalKeys.Add(logicalKey);
                if (batchPhysicalKeys.Count < _configuration.BatchSize)
                    continue;
                await foreach (var entry in ReadPhysicalBatchAsync<T>(batchPhysicalKeys, batchLogicalKeys, cancellationToken))
                    yield return entry;
                batchPhysicalKeys.Clear();
                batchLogicalKeys.Clear();
            }
        }

        if (batchPhysicalKeys.Count == 0)
            yield break;
        await foreach (var entry in ReadPhysicalBatchAsync<T>(batchPhysicalKeys, batchLogicalKeys, cancellationToken))
            yield return entry;
    }

    /// <summary>
    /// 流式读取一批物理键并保留逻辑键和命中状态。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="logicalKeys">对应的逻辑键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存条目的异步序列。</returns>
    private async IAsyncEnumerable<CacheEntry<T>> ReadPhysicalBatchAsync<T>(IList<string> physicalKeys,
        IList<string> logicalKeys,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var values = await GetAllPhysicalValuesAsync<T>(physicalKeys, cancellationToken);
        if (values == null)
        {
            for (var index = 0; index < physicalKeys.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var value = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                    () => _provider.GetAsync<T>(physicalKeys[index], cancellationToken));
                // 逐键回退也必须保留扫描候选键；HasValue 只表达命中状态，不能丢弃竞态 miss。
                yield return new CacheEntry<T>(logicalKeys[index], value.HasValue ? value.Value : default,
                    value.HasValue);
            }
            yield break;
        }

        for (var index = 0; index < physicalKeys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var physicalKey = physicalKeys[index];
            if (values.TryGetValue(physicalKey, out var value))
            {
                // CacheValue 的 HasValue 表示键是否命中，不能用值是否为 null 筛除 null 命中。
                yield return new CacheEntry<T>(logicalKeys[index], value.HasValue ? value.Value : default,
                    value.HasValue);
            }
            else
                yield return new CacheEntry<T>(logicalKeys[index], default, false);
        }
    }

    #endregion

    #region TrySet

    /// <inheritdoc />
    public bool TrySet<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        return TrySet(key.Key, value, options);
    }

    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, CacheOptions options = null)
    {
        return WriteValue(PhysicalKey(key), value, GetExpiration(options), true);
    }

    #endregion

    #region TrySetAsync

    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await TrySetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        return await WriteValueAsync(PhysicalKey(key), value, GetExpiration(options), true, cancellationToken);
    }

    #endregion

    #region Set

    /// <inheritdoc />
    public void Set<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        Set(key.Key, value, options);
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, CacheOptions options = null)
    {
        WriteValue(PhysicalKey(key), value, GetExpiration(options));
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null)
    {
        SetAll(ToItems(items), options);
    }

    /// <summary>
    /// 转换为缓存项集合
    /// </summary>
    /// <typeparam name="T">值元素类型</typeparam>
    /// <param name="items">字典</param>
    /// <returns>以字符串缓存键为键的缓存项字典。</returns>
    private IDictionary<string, T> ToItems<T>(IDictionary<CacheKey, T> items)
    {
        items.CheckNull(nameof(items));
        return items.Select(item =>
        {
            item.Key.Validate();
            return new KeyValuePair<string, T>(item.Key.Key, item.Value);
        }).ToDictionary(t => t.Key, t => t.Value);
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<string, T> items, CacheOptions options = null)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        var expiration = GetExpiration(options);
        var batch = new Dictionary<string, T>(_configuration.BatchSize);
        foreach (var item in items)
        {
            batch.Add(PhysicalKey(item.Key), item.Value);
            if (batch.Count < _configuration.BatchSize)
                continue;
            SetPhysicalBatch(batch, expiration);
            batch = new Dictionary<string, T>(_configuration.BatchSize);
        }
        if (batch.Count > 0)
            SetPhysicalBatch(batch, expiration);
    }

    #endregion

    #region SetAsync

    /// <inheritdoc />
    public async Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await SetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        await WriteValueAsync(PhysicalKey(key), value, GetExpiration(options), false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        await SetAllAsync(ToItems(items), options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        var expiration = GetExpiration(options);
        var batch = new Dictionary<string, T>(_configuration.BatchSize);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(PhysicalKey(item.Key), item.Value);
            if (batch.Count < _configuration.BatchSize)
                continue;
            await SetPhysicalBatchAsync(batch, expiration, cancellationToken);
            batch = new Dictionary<string, T>(_configuration.BatchSize);
        }
        if (batch.Count > 0)
            await SetPhysicalBatchAsync(batch, expiration, cancellationToken);
    }

    #endregion

    #region Remove

    /// <inheritdoc />
    public void Remove(CacheKey key)
    {
        key.Validate();
        Remove(key.Key);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        DeleteValue(PhysicalKey(key));
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys)
    {
        RemoveAll(ToKeys(keys));
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        RemovePhysicalKeys(keys.Select(PhysicalKey));
    }

    #endregion

    #region RemoveAsync

    /// <inheritdoc />
    public async Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        await RemoveAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await DeleteValueAsync(PhysicalKey(key), cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default)
    {
        await RemoveAllAsync(ToKeys(keys), cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        await RemovePhysicalKeysAsync(keys.Select(PhysicalKey), cancellationToken);
    }

    #endregion

    #region RemoveByPrefix

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        RemoveByPrefixWithBudget(prefix,
            new CacheScanBudget(_configuration.Resources));
    }

    /// <summary>
    /// 按逻辑键前缀删除缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal void RemoveByPrefixWithBudget(string prefix, CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (prefix.IsEmpty())
            return;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        RemovePhysicalKeys(FindKeys(key => key.StartsWith(prefix, StringComparison.Ordinal),
            CacheKeyFormat.GetPrefixPattern(_configuration, prefix), scanBudget,
            CacheKeyFormat.GetEncodedLogicalPrefix(prefix)));
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        await RemoveByPrefixWithBudgetAsync(prefix, cancellationToken,
            new CacheScanBudget(_configuration.Resources)).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步按逻辑键前缀删除缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal async Task RemoveByPrefixWithBudgetAsync(string prefix, CancellationToken cancellationToken,
        CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (prefix.IsEmpty())
            return;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        await RemoveMatchingKeysAsync(key => key.StartsWith(prefix, StringComparison.Ordinal), cancellationToken,
            CacheKeyFormat.GetPrefixPattern(_configuration, prefix), scanBudget,
            CacheKeyFormat.GetEncodedLogicalPrefix(prefix));
    }

    #endregion

    #region RemoveByPattern

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        RemoveByPatternWithBudget(pattern,
            new CacheScanBudget(_configuration.Resources));
    }

    /// <summary>
    /// 按逻辑键模式删除缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="pattern">逻辑键匹配模式。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal void RemoveByPatternWithBudget(string pattern, CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (pattern.IsEmpty())
            return;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            Remove(matcher.LiteralPrefix);
            return;
        }
        RemovePhysicalKeys(FindKeys(key => matcher.IsMatch(key),
            CacheKeyFormat.GetPatternScanPattern(_configuration, matcher), scanBudget));
    }

    #endregion

    #region RemoveByPatternAsync

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        await RemoveByPatternWithBudgetAsync(pattern, cancellationToken,
            new CacheScanBudget(_configuration.Resources)).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步按逻辑键模式删除缓存，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="pattern">逻辑键匹配模式。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal async Task RemoveByPatternWithBudgetAsync(string pattern, CancellationToken cancellationToken,
        CacheScanBudget scanBudget)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (pattern.IsEmpty())
            return;
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RemoveAsync(matcher.LiteralPrefix, cancellationToken);
            return;
        }
        await RemoveMatchingKeysAsync(key => matcher.IsMatch(key), cancellationToken,
            CacheKeyFormat.GetPatternScanPattern(_configuration, matcher), scanBudget);
    }

    #endregion

    #region Clear

    /// <inheritdoc />
    public void Clear()
    {
        ClearWithBudget(new CacheScanBudget(_configuration.Resources));
    }

    /// <summary>
    /// 清理当前缓存命名空间，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal void ClearWithBudget(CacheScanBudget scanBudget)
    {
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        RemovePhysicalKeys(FindKeys(_ => true, scanBudget: scanBudget));
    }

    #endregion

    #region ClearAsync

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ClearWithBudgetAsync(cancellationToken,
            new CacheScanBudget(_configuration.Resources)).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步清理当前缓存命名空间，并使用调用方提供的扫描预算。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次操作使用的扫描预算。</param>
    internal async Task ClearWithBudgetAsync(CancellationToken cancellationToken, CacheScanBudget scanBudget)
    {
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        await RemoveMatchingKeysAsync(_ => true, cancellationToken, scanBudget: scanBudget)
            .ConfigureAwait(false);
    }

    #endregion

    /// <summary>
    /// 将逻辑缓存键转换为当前命名空间下的物理键。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <returns>命名空间隔离后的物理键。</returns>
    private string PhysicalKey(string key) => CacheKeyFormat.GetPhysicalKey(_configuration, key);

    /// <summary>
    /// 扫描并筛选当前命名空间下的物理键。
    /// </summary>
    /// <param name="predicate">逻辑键匹配条件。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    /// <param name="scanBudget">可选的扫描预算。</param>
    /// <param name="encodedLogicalPrefix">可选的编码逻辑前缀。</param>
    /// <returns>符合条件的物理键序列。</returns>
    private IEnumerable<string> FindKeys(Func<string, bool> predicate, string scanPattern = null,
        CacheScanBudget scanBudget = null, string encodedLogicalPrefix = null)
    {
        Validate();
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        scanBudget ??= new CacheScanBudget(_configuration.Resources);
        foreach (var physicalKey in EnumerateNamespaceKeys(scanPattern, scanBudget))
            if ((encodedLogicalPrefix == null
                    ? CacheKeyFormat.TryGetLogicalKey(hashContext, physicalKey, out var logicalKey)
                    : CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physicalKey, encodedLogicalPrefix,
                        out logicalKey)))
            {
                scanBudget.Consume();
                if (predicate(logicalKey))
                    yield return physicalKey;
            }
    }

    /// <summary>
    /// 枚举当前缓存命名空间中的物理键。
    /// </summary>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    /// <param name="scanBudget">本次扫描使用的预算。</param>
    /// <returns>命名空间内的物理键序列。</returns>
    private IEnumerable<string> EnumerateNamespaceKeys(string scanPattern, CacheScanBudget scanBudget)
    {
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));
        var namespacePattern = CacheKeyFormat.GetNamespacePattern(_configuration);
        var pattern = scanPattern ?? namespacePattern;
        if (_cachingProvider.Database is IDatabase database)
        {
            var scanTopology = ValidateClusterScanTopology(database);
            var scanServers = scanTopology.Servers;
            foreach (var server in scanServers)
            {
                if (!server.IsConnected)
                    throw new InvalidOperationException("缓存扫描主节点不可用。");
                foreach (var key in EnumerateRedisKeys(server, database.Database, pattern, scanBudget))
                    yield return key;
            }
            EnsureClusterScanTopologyUnchanged(database, scanTopology.SlotOwnership);
            yield break;
        }
        var namespacePrefix = namespacePattern.Substring(0, namespacePattern.Length - 1);
        foreach (var key in _cachingProvider.GetAllKeysByPrefix(namespacePrefix))
        {
            CacheCommandProbe.RecordScanItem();
            yield return key;
        }
    }

    /// <summary>
    /// 异步扫描并处理当前命名空间下符合条件的键。
    /// </summary>
    /// <param name="predicate">逻辑键匹配条件。</param>
    /// <param name="visit">处理匹配物理键的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    /// <param name="scanBudget">可选的扫描预算。</param>
    /// <param name="encodedLogicalPrefix">可选的编码逻辑前缀。</param>
    private async Task VisitKeysAsync(Func<string, bool> predicate, Func<string, Task> visit,
        CancellationToken cancellationToken, string scanPattern = null, CacheScanBudget scanBudget = null,
        string encodedLogicalPrefix = null)
    {
        Validate();
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        scanBudget ??= new CacheScanBudget(_configuration.Resources);
        var namespacePattern = CacheKeyFormat.GetNamespacePattern(_configuration);
        var pattern = scanPattern ?? namespacePattern;
        if (_cachingProvider.Database is IDatabase database)
        {
            var scanTopology = await ValidateClusterScanTopologyAsync(database, cancellationToken);
            var scanServers = scanTopology.Servers;
            foreach (var server in scanServers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!server.IsConnected)
                    throw new InvalidOperationException("缓存扫描主节点不可用。");
                await foreach (var redisKey in EnumerateRedisKeysAsync(server, database.Database, pattern,
                                   scanBudget, cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var key = redisKey.ToString();
                    if ((encodedLogicalPrefix == null
                            ? CacheKeyFormat.TryGetLogicalKey(hashContext, key, out var logicalKey)
                            : CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, key, encodedLogicalPrefix,
                                out logicalKey)))
                    {
                        scanBudget.Consume();
                        if (predicate(logicalKey))
                            await visit(key);
                    }
                }
            }
            await EnsureClusterScanTopologyUnchangedAsync(database, scanTopology.SlotOwnership,
                cancellationToken);
            return;
        }
        var namespacePrefix = namespacePattern.Substring(0, namespacePattern.Length - 1);
        await foreach (var key in EnumerateProviderKeysAsync(namespacePrefix, cancellationToken, scanBudget)
                           .ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((encodedLogicalPrefix == null
                    ? CacheKeyFormat.TryGetLogicalKey(hashContext, key, out var logicalKey)
                    : CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, key, encodedLogicalPrefix,
                        out logicalKey)))
            {
                scanBudget.Consume();
                if (predicate(logicalKey))
                    await visit(key);
            }
        }
    }

    /// <summary>
    /// 使用 Redis SCAN 同步枚举匹配的键。
    /// </summary>
    /// <param name="server">扫描目标服务器。</param>
    /// <param name="database">Redis 数据库编号。</param>
    /// <param name="pattern">物理键扫描模式。</param>
    /// <param name="scanBudget">本次扫描使用的预算。</param>
    /// <returns>扫描得到的物理键序列。</returns>
    private IEnumerable<string> EnumerateRedisKeys(IServer server, int database, string pattern,
        CacheScanBudget scanBudget)
    {
        if (server == null)
            throw new ArgumentNullException(nameof(server));
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        long cursor = 0;
        do
        {
            scanBudget.ConsumePage();
            var response = (RedisResult[])server.Execute("SCAN", cursor, "MATCH", pattern,
                "COUNT", _configuration.BatchSize);
            if (response == null || response.Length != 2)
                throw new InvalidOperationException("Redis SCAN 返回了无效响应。");

            cursor = (long)response[0];
            var keys = response[1].IsNull ? Array.Empty<RedisResult>() : (RedisResult[])response[1];
            CacheCommandProbe.RecordScanPage(keys.Length);
            foreach (var key in keys)
                yield return key.ToString();
        }
        while (cursor != 0);
    }

    /// <summary>
    /// 使用 Redis SCAN 异步枚举匹配的键。
    /// </summary>
    /// <param name="server">扫描目标服务器。</param>
    /// <param name="database">Redis 数据库编号。</param>
    /// <param name="pattern">物理键扫描模式。</param>
    /// <param name="scanBudget">本次扫描使用的预算。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>扫描得到的物理键异步序列。</returns>
    private async IAsyncEnumerable<string> EnumerateRedisKeysAsync(IServer server, int database,
        string pattern, CacheScanBudget scanBudget,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (server == null)
            throw new ArgumentNullException(nameof(server));
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        long cursor = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanBudget.ConsumePage();
            var response = (RedisResult[])await server.ExecuteAsync("SCAN", cursor, "MATCH", pattern,
                "COUNT", _configuration.BatchSize).ConfigureAwait(false);
            if (response == null || response.Length != 2)
                throw new InvalidOperationException("Redis SCAN 返回了无效响应。");

            cursor = (long)response[0];
            var keys = response[1].IsNull ? Array.Empty<RedisResult>() : (RedisResult[])response[1];
            CacheCommandProbe.RecordScanPage(keys.Length);
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return key.ToString();
            }
        }
        while (cursor != 0);
    }

    /// <summary>
    /// 使用 Provider 键流或分页接口枚举命名空间内的键。
    /// </summary>
    /// <param name="namespacePrefix">当前命名空间的物理键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanBudget">本次扫描使用的预算。</param>
    /// <returns>命名空间内物理键的异步序列。</returns>
    private async IAsyncEnumerable<string> EnumerateProviderKeysAsync(string namespacePrefix,
        [EnumeratorCancellation] CancellationToken cancellationToken, CacheScanBudget scanBudget)
    {
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        if (_keyStream != null)
        {
            await foreach (var key in _keyStream(namespacePrefix, cancellationToken)
                               .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                CacheCommandProbe.RecordScanItem();
                yield return key;
            }
            yield break;
        }

        if (_cachingProvider is ICacheKeyPageStreamProvider keyPageStreamProvider)
        {
            await foreach (var page in keyPageStreamProvider.GetAllKeyPagesByPrefixStreamAsync(
                               namespacePrefix, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (page == null)
                    throw new InvalidOperationException("缓存提供器返回了空的键页。");
                scanBudget.ConsumePage();
                CacheCommandProbe.RecordScanPage(page.Count);
                foreach (var key in page)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CacheCommandProbe.RecordScanItem();
                    yield return key;
                }
            }
            yield break;
        }

        if (_cachingProvider is ICacheKeyStreamProvider keyStreamProvider)
        {
            await foreach (var key in keyStreamProvider.GetAllKeysByPrefixStreamAsync(
                               namespacePrefix, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                CacheCommandProbe.RecordScanItem();
                yield return key;
            }
            yield break;
        }

        var keys = await _cachingProvider.GetAllKeysByPrefixAsync(namespacePrefix, cancellationToken);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CacheCommandProbe.RecordScanItem();
            yield return key;
        }
    }

    /// <summary>
    /// 异步扫描、筛选并处理匹配的物理键。
    /// </summary>
    /// <param name="predicate">逻辑键匹配条件。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    /// <param name="scanBudget">可选的扫描预算。</param>
    /// <param name="encodedLogicalPrefix">可选的编码逻辑前缀。</param>
    private async Task RemoveMatchingKeysAsync(Func<string, bool> predicate, CancellationToken cancellationToken,
        string scanPattern = null, CacheScanBudget scanBudget = null, string encodedLogicalPrefix = null)
    {
        var batch = new List<string>(_configuration.BatchSize);
        await VisitKeysAsync(predicate, async key =>
        {
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize) return;
            await RemovePhysicalBatchAsync(batch, cancellationToken);
            batch.Clear();
        }, cancellationToken, scanPattern, scanBudget, encodedLogicalPrefix);
        if (batch.Count > 0)
            await RemovePhysicalBatchAsync(batch, cancellationToken);
    }

    /// <summary>
    /// 分批删除物理缓存键及其租约键。
    /// </summary>
    /// <param name="keys">待删除的物理键序列。</param>
    private void RemovePhysicalKeys(IEnumerable<string> keys)
    {
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            RemovePhysicalBatch(batch);
            batch = new List<string>(_configuration.BatchSize);
        }
        if (batch.Count > 0)
            RemovePhysicalBatch(batch);
    }

    /// <summary>
    /// 异步分批删除物理缓存键及其租约键。
    /// </summary>
    /// <param name="keys">待删除的物理键序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RemovePhysicalKeysAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            await RemovePhysicalBatchAsync(batch, cancellationToken);
            batch = new List<string>(_configuration.BatchSize);
        }
        if (batch.Count > 0)
            await RemovePhysicalBatchAsync(batch, cancellationToken);
    }

}
