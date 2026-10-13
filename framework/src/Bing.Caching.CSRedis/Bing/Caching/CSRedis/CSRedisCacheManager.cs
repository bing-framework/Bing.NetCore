using System.Text;
using System.Collections.Generic;
using CSRedis;
using Newtonsoft.Json;

namespace Bing.Caching.CSRedis;

/// <summary>
/// 使用 CSRedis 客户端访问缓存。
/// </summary>
public sealed partial class CSRedisCacheManager : IRedisCache
{
    /// <summary>
    /// 用于读写缓存数据的 CSRedis 客户端。
    /// </summary>
    private readonly CSRedisClient _client;
    /// <summary>
    /// 用于扫描缓存键的主节点客户端。
    /// </summary>
    private readonly IReadOnlyList<CSRedisClient> _scanClients;
    /// <summary>
    /// 客户端分片的键路由函数。
    /// </summary>
    private readonly Func<string, int> _shardNodeIndex;
    /// <summary>
    /// 当前缓存实例的配置。
    /// </summary>
    private readonly CacheConfiguration _configuration;
    /// <summary>
    /// 按物理键管理的进程内回源门闩。
    /// </summary>
    private readonly Dictionary<string, LocalGate> _gates = new Dictionary<string, LocalGate>();
    /// <summary>
    /// 当前异步执行流中正在回源的逻辑键集合。
    /// </summary>
    private readonly AsyncLocal<HashSet<string>> _activeFactories = new AsyncLocal<HashSet<string>>();

    /// <summary>
    /// 表示单个物理缓存键的本地回源门闩。
    /// </summary>
    private sealed class LocalGate
    {
        /// <summary>
        /// 串行化同键回源的信号量。
        /// </summary>
        public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
        /// <summary>
        /// 当前引用该门闩的调用数。
        /// </summary>
        public int References;
    }

    /// <summary>
    /// 获取指定键的本地回源门闩。
    /// </summary>
    /// <param name="key">物理缓存键。</param>
    /// <returns>对应的回源门闩。</returns>
    private LocalGate GetGate(string key)
    {
        lock (_gates)
        {
            if (!_gates.TryGetValue(key, out var gate)) _gates[key] = gate = new LocalGate();
            gate.References++;
            return gate;
        }
    }

    /// <summary>
    /// 释放指定键的本地回源门闩引用。
    /// </summary>
    /// <param name="key">物理缓存键。</param>
    /// <param name="gate">待释放的回源门闩。</param>
    /// <param name="entered">调用方是否已进入门闩。</param>
    private void ReleaseGate(string key, LocalGate gate, bool entered)
    {
        if (entered)
            gate.Semaphore.Release();
        lock (_gates)
        {
            if (--gate.References == 0)
            {
                _gates.Remove(key);
                gate.Semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// 初始化一个 <see cref="CSRedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="client">用于读写缓存的 CSRedis 客户端。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="scanClients">可选的主节点扫描客户端集合。</param>
    /// <param name="shardNodeIndex">可选的客户端分片路由函数。</param>
    /// <remarks>自定义 NodeRule 分片时，路由函数需返回对应节点索引；客户端生命周期由调用方管理。</remarks>
    public CSRedisCacheManager(CSRedisClient client, CacheConfiguration configuration,
        IReadOnlyList<CSRedisClient> scanClients = null, Func<string, int> shardNodeIndex = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (_client.Nodes.Values.Any(node => !string.IsNullOrEmpty(node.Prefix)))
            throw new ArgumentException("CSRedis 客户端不能配置 prefix；缓存命名空间由 CacheConfiguration 管理。", nameof(client));
        if (configuration.Topology == CacheTopology.SingleNode)
        {
            if (_client.Nodes.Count != 1)
                throw new ArgumentException("单节点配置要求 CSRedisClient 恰好包含一个节点。", nameof(client));
            _scanClients = new[] { _client };
        }
        else
        {
            if (scanClients == null || scanClients.Count == 0 ||
                scanClients.Any(scanClient => scanClient == null ||
                    (configuration.Topology == CacheTopology.ClientSharded && scanClient.Nodes.Count != 1) ||
                    scanClient.Nodes.Values.Any(node => !string.IsNullOrEmpty(node.Prefix))))
                throw new ArgumentException("多节点配置必须显式提供无 prefix 的主节点扫描客户端。", nameof(scanClients));
            _scanClients = scanClients.ToArray();
            if (configuration.Topology == CacheTopology.ClientSharded &&
                _scanClients.Count != configuration.ExpectedShardCount)
                throw new ArgumentException("扫描客户端数量与预期分片数不一致。", nameof(scanClients));
            if (configuration.Topology == CacheTopology.ClientSharded)
                CacheShardTopology.Validate(_scanClients.Select(scanClient =>
                {
                    using var connection = scanClient.Nodes.Values.Single().Get();
                    return connection.Value.Info("server");
                }), configuration.ExpectedShardCount.Value);
        }
        _shardNodeIndex = shardNodeIndex ?? (key => CacheClusterTopology.GetHashSlot(key) % _scanClients.Count);
    }

    /// <summary>
    /// 获取逻辑键对应的物理键。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <returns>隔离后的物理缓存键。</returns>
    private string Key(string key) => CacheKeyFormat.GetPhysicalKey(_configuration, key);

    /// <summary>
    /// 获取本次写入采用的有效期。
    /// </summary>
    /// <param name="options">缓存选项。</param>
    /// <returns>向上取整到毫秒的有效期。</returns>
    private TimeSpan Expiration(CacheOptions options)
    {
        var value = options?.Expiration ?? _configuration.DefaultExpiration;
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "缓存有效期必须大于零。");
        var milliseconds = Math.Ceiling(value.TotalMilliseconds);
        if (milliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(options), "缓存有效期超出 Redis 毫秒参数范围。");
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// 将缓存值序列化为 JSON。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="value">待序列化的缓存值。</param>
    /// <returns>JSON 文本。</returns>
    private static string Value<T>(T value) => JsonConvert.SerializeObject(value);

    /// <summary>
    /// 将缓存字节反序列化为指定类型。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="bytes">缓存值字节；未命中时为 <see langword="null"/>。</param>
    /// <returns>反序列化结果；未命中时返回默认值。</returns>
    private static T Read<T>(byte[] bytes) => bytes == null ? default : JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes));

    /// <summary>
    /// 将缓存字节反序列化为指定类型。
    /// </summary>
    /// <param name="bytes">缓存值字节；未命中时为 <see langword="null"/>。</param>
    /// <param name="type">目标运行时类型。</param>
    /// <returns>反序列化结果；未命中时返回 <see langword="null"/>。</returns>
    private static object Read(byte[] bytes, Type type) => bytes == null ? null : JsonConvert.DeserializeObject(Encoding.UTF8.GetString(bytes), type);

    /// <summary>
    /// 将有效期转换为毫秒数。
    /// </summary>
    /// <param name="value">缓存有效期。</param>
    /// <returns>有效期毫秒数。</returns>
    private static long Milliseconds(TimeSpan value) => (long)value.TotalMilliseconds;

    /// <summary>
    /// 写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="value">待写入的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyWhenMissing">是否仅在键不存在时写入。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    /// <remarks>使用 Redis 毫秒级 TTL 命令。</remarks>
    private bool Write<T>(string key, T value, TimeSpan expiration, bool onlyWhenMissing = false)
    {
        var command = onlyWhenMissing
            ? "local r=redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX') if r then redis.call('DEL', KEYS[1] .. ':lease') end return r"
            : "local r=redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2]) redis.call('DEL', KEYS[1] .. ':lease') return r";
        return _client.Eval(command, Key(key), Value(value), Milliseconds(expiration)) != null;
    }

    /// <summary>
    /// 异步写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="value">待写入的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyWhenMissing">是否仅在键不存在时写入。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    private async Task<bool> WriteAsync<T>(string key, T value, TimeSpan expiration, bool onlyWhenMissing, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var command = onlyWhenMissing
            ? "local r=redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX') if r then redis.call('DEL', KEYS[1] .. ':lease') end return r"
            : "local r=redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2]) redis.call('DEL', KEYS[1] .. ':lease') return r";
        return await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
                () => _client.EvalAsync(command, Key(key), Value(value), Milliseconds(expiration)))
            .ConfigureAwait(false) != null;
    }

    /// <inheritdoc />
    public bool Exists(CacheKey key) { key.Validate(); return Exists(key.Key); }
    /// <inheritdoc />
    public bool Exists(string key) => _client.Exists(Key(key));
    /// <inheritdoc />
    public Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default) { key.Validate(); return ExistsAsync(key.Key, cancellationToken); }
    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _client.ExistsAsync(Key(key));
    }

    /// <inheritdoc />
    public T Get<T>(CacheKey key) { key.Validate(); return Get<T>(key.Key); }
    /// <inheritdoc />
    public T Get<T>(string key) => Read<T>(_client.Get<byte[]>(Key(key)));
    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null) { key.Validate(); return Get(key.Key, dataRetriever, options); }
    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null)
    {
        if (dataRetriever == null) throw new ArgumentNullException(nameof(dataRetriever));
        return GetOrCreateAsync(key, _ => Task.FromResult(dataRetriever()), options, CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default) { key.Validate(); return GetAsync<T>(key.Key, cancellationToken); }
    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Read<T>(await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => _client.GetAsync<byte[]>(Key(key)))
            .ConfigureAwait(false));
    }
    /// <inheritdoc />
    public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        cancellationToken.ThrowIfCancellationRequested();
        return Read(await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => _client.GetAsync<byte[]>(Key(key)))
            .ConfigureAwait(false), type);
    }
    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    { key.Validate(); return GetAsync(key.Key, dataRetriever, options, cancellationToken); }
    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (dataRetriever == null) throw new ArgumentNullException(nameof(dataRetriever));
        return await GetOrCreateAsync(key, _ => dataRetriever(), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
        => GetOrCreateAsync(key, dataRetriever, options, cancellationToken);

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    { key.Validate(); return GetOrCreateAsync(key.Key, dataRetriever, options, cancellationToken); }

    /// <summary>
    /// 获取缓存值，并在未命中时协调执行回源。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="retriever">接收取消令牌的异步回源委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存命中值或回源结果。</returns>
    private async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> retriever,
        CacheOptions options, CancellationToken cancellationToken)
    {
        if (retriever == null) throw new ArgumentNullException(nameof(retriever));
        if (_activeFactories.Value?.Contains(key) == true)
            throw new InvalidOperationException("同键缓存回源不能递归调用。");
        cancellationToken.ThrowIfCancellationRequested();
        var physical = Key(key);
        var bytes = await _client.GetAsync<byte[]>(physical).ConfigureAwait(false);
        if (bytes != null) return Read<T>(bytes);

        var gate = GetGate(physical);
        var entered = false;
        try
        {
            if (!await gate.Semaphore.WaitAsync(_configuration.Coordination.WaitTimeout, cancellationToken).ConfigureAwait(false))
                throw new TimeoutException("等待本地缓存回源协调超时。");
            entered = true;
            bytes = await _client.GetAsync<byte[]>(physical).ConfigureAwait(false);
            if (bytes != null) return Read<T>(bytes);

            using var admission = await CacheFactoryAdmission.EnterAsync(_configuration.Resources, _configuration.Coordination.WaitTimeout, cancellationToken);
            var token = cancellationToken;
            var owner = Guid.NewGuid().ToString("N");
            var deadline = DateTime.UtcNow + _configuration.Coordination.WaitTimeout;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var acquired = await _client.EvalAsync(
                    "return redis.call('SET', KEYS[1] .. ':lease', ARGV[1], 'PX', ARGV[2], 'NX')",
                    physical, owner, Milliseconds(_configuration.Coordination.LeaseDuration)).ConfigureAwait(false);
                if (acquired != null) break;
                bytes = await _client.GetAsync<byte[]>(physical).ConfigureAwait(false);
                if (bytes != null) return Read<T>(bytes);
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("等待缓存回源租约超时。");
                await Task.Delay(100 + (Environment.TickCount & 127), token).ConfigureAwait(false);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_configuration.Coordination.RetrievalTimeout);
            token = timeout.Token;
            using var renewal = CancellationTokenSource.CreateLinkedTokenSource(token);
            var renewTask = RenewLeaseAsync(physical, owner, renewal, timeout);
            Exception primary = null;
            try
            {
                bytes = await _client.GetAsync<byte[]>(physical).ConfigureAwait(false);
                if (bytes != null) return Read<T>(bytes);
                var previous = _activeFactories.Value;
                var active = previous == null ? new HashSet<string>() : new HashSet<string>(previous);
                active.Add(key);
                _activeFactories.Value = active;
                T value;
                try
                {
                    value = await CacheFactoryExecution.InvokeAsync(retriever, token, cancellationToken).ConfigureAwait(false);
                }
                finally { _activeFactories.Value = previous; }
                CacheFactoryExecution.ThrowIfCancelled(token, cancellationToken);
                var committed = await _client.EvalAsync(
                    "if redis.call('GET', KEYS[1] .. ':lease') ~= ARGV[1] then return 0 end " +
                    "redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3]) " +
                    "redis.call('DEL', KEYS[1] .. ':lease') return 1",
                    physical, owner, Value(value), Milliseconds(Expiration(options))).ConfigureAwait(false);
                if (Convert.ToInt64(committed) != 1)
                    throw new InvalidOperationException("缓存回源租约已失效，结果未写回。");
                return value;
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                renewal.Cancel();
                await CacheFactoryExecution.CleanupAsync(async () =>
                {
                await renewTask.ConfigureAwait(false);
                await _client.EvalAsync(
                    "if redis.call('GET', KEYS[1] .. ':lease') == ARGV[1] then return redis.call('DEL', KEYS[1] .. ':lease') end return 0",
                    physical, owner).ConfigureAwait(false);
                }, primary, _configuration.Coordination.WaitTimeout).ConfigureAwait(false);
            }
        }
        finally
        {
            ReleaseGate(physical, gate, entered);
        }
    }

    /// <summary>
    /// 定期续租并在租约失效时取消回源操作。
    /// </summary>
    /// <param name="physical">数据物理键。</param>
    /// <param name="owner">租约所有权令牌。</param>
    /// <param name="stop">停止续租的取消源。</param>
    /// <param name="operation">回源操作的取消源。</param>
    private async Task RenewLeaseAsync(string physical, string owner, CancellationTokenSource stop,
        CancellationTokenSource operation)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(_configuration.Coordination.RenewalInterval, stop.Token).ConfigureAwait(false);
                var renewed = await _client.EvalAsync(
                    "if redis.call('GET', KEYS[1] .. ':lease') == ARGV[1] then return redis.call('PEXPIRE', KEYS[1] .. ':lease', ARGV[2]) end return 0",
                    physical, owner, Milliseconds(_configuration.Coordination.LeaseDuration)).ConfigureAwait(false);
                if (Convert.ToInt64(renewed) != 1) { operation.Cancel(); return; }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch { operation.Cancel(); }
    }

    /// <summary>
    /// 将类型化缓存键转换为逻辑键数组。
    /// </summary>
    /// <param name="keys">类型化缓存键集合。</param>
    /// <returns>按输入顺序排列的逻辑键。</returns>
    private static string[] Keys(IEnumerable<CacheKey> keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        return keys.Select(key => { key.Validate(); return key.Key; }).ToArray();
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys) => GetAll<T>(Keys(keys));
    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        var input = keys.Select(Key).ToArray();
        var result = new List<T>(input.Length);
        for (var offset = 0; offset < input.Length; offset += _configuration.BatchSize)
        {
            var count = Math.Min(_configuration.BatchSize, input.Length - offset);
            var batch = new string[count];
            Array.Copy(input, offset, batch, 0, count);
            if (_configuration.Topology == CacheTopology.RedisCluster)
                result.AddRange(ReadClusterValues<T>(batch));
            else if (_configuration.Topology == CacheTopology.ClientSharded)
                result.AddRange(ReadShardedValuesAsync<T>(batch, CancellationToken.None).GetAwaiter().GetResult());
            else
                result.AddRange(ReadValidatedBatch<T>(ReadRawBatch(batch)));
        }
        return result;
    }
    /// <inheritdoc />
    public Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => GetAllAsync<T>(Keys(keys), cancellationToken);
    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        var result = CacheCollectionCount.TryGetCount(keys, out var count)
            ? new List<T>(count)
            : new List<T>();
        var logicalBatch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logicalBatch.Add(key);
            if (logicalBatch.Count < _configuration.BatchSize)
                continue;
            var physicalKeys = CacheKeyFormat.GetPhysicalKeys(_configuration, logicalBatch);
            result.AddRange(await ReadValuesBatchAsync<T>(physicalKeys, cancellationToken)
                .ConfigureAwait(false));
            logicalBatch.Clear();
        }
        if (logicalBatch.Count > 0)
        {
            var physicalKeys = CacheKeyFormat.GetPhysicalKeys(_configuration, logicalBatch);
            result.AddRange(await ReadValuesBatchAsync<T>(physicalKeys, cancellationToken)
                .ConfigureAwait(false));
        }
        return result;
    }

    /// <summary>
    /// 扫描并还原当前命名空间中的逻辑键。
    /// </summary>
    /// <param name="pattern">Redis SCAN 模式提示。</param>
    /// <returns>通过物理键校验的逻辑键。</returns>
    /// <remarks>SCAN 模式仅用于减少返回量，每个结果仍会校验命名空间。</remarks>
    private IEnumerable<string> Scan(string pattern)
    {
        var topology = ValidateScanTopology();
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanClient in _scanClients)
        foreach (var entry in scanClient.Nodes)
        {
            var identity = entry.Key;
            if (_configuration.Topology == CacheTopology.RedisCluster)
            {
                using var identityConnection = entry.Value.Get();
                identity = ClusterText(identityConnection.Value.Call("CLUSTER", new[] { "MYID" }));
            }
            if (!visitedNodes.Add(identity)) continue;
            var node = entry.Value;
            using var connection = node.Get();
            if (!string.Equals(connection.Value.Role().RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
            long cursor = 0;
            do
            {
                budget.ConsumePage();
                global::CSRedis.RedisScan<string> page =
                    connection.Value.Scan(cursor, pattern, _configuration.BatchSize);
                cursor = page.Cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physical in page.Items)
                {
                    CacheCommandProbe.RecordScanItem();
                    if (CacheKeyFormat.TryGetLogicalKey(hashContext, physical, out var logical))
                    {
                        budget.Consume();
                        yield return logical;
                    }
                }
            } while (cursor != 0);
            if (!string.Equals(connection.Value.Role().RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
        }
        CacheClusterTopology.EnsureUnchanged(topology, ValidateScanTopology());
    }

    /// <summary>
    /// 扫描并返回符合逻辑前缀的物理键。
    /// </summary>
    /// <param name="pattern">Redis SCAN 模式提示。</param>
    /// <param name="logicalPrefix">待匹配的逻辑键前缀。</param>
    /// <returns>通过前缀和物理键校验的物理键。</returns>
    /// <remarks>直接返回物理键，避免先解码逻辑键再重新编码。</remarks>
    private IEnumerable<string> ScanPhysicalPrefix(string pattern, string logicalPrefix)
    {
        var topology = ValidateScanTopology();
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(logicalPrefix);
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanClient in _scanClients)
        foreach (var entry in scanClient.Nodes)
        {
            var identity = entry.Key;
            if (_configuration.Topology == CacheTopology.RedisCluster)
            {
                using var identityConnection = entry.Value.Get();
                identity = ClusterText(identityConnection.Value.Call("CLUSTER", new[] { "MYID" }));
            }
            if (!visitedNodes.Add(identity)) continue;
            using var connection = entry.Value.Get();
            if (!string.Equals(connection.Value.Role().RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
            long cursor = 0;
            do
            {
                budget.ConsumePage();
                var page = connection.Value.Scan(cursor, pattern, _configuration.BatchSize);
                cursor = page.Cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physical in page.Items)
                {
                    CacheCommandProbe.RecordScanItem();
                    if (CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, logicalPrefix, encodedPrefix))
                    {
                        budget.Consume();
                        yield return physical;
                    }
                }
            } while (cursor != 0);
            if (!string.Equals(connection.Value.Role().RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
        }
        CacheClusterTopology.EnsureUnchanged(topology, ValidateScanTopology());
    }

    /// <summary>
    /// 异步扫描缓存键并分批传递已验证的逻辑键。
    /// </summary>
    /// <param name="pattern">Redis SCAN 模式提示。</param>
    /// <param name="visitor">处理一批逻辑键的异步委托。</param>
    /// <param name="token">取消令牌。</param>
    private async Task VisitAsync(string pattern,
        Func<IReadOnlyList<string>, CancellationToken, Task> visitor, CancellationToken token)
    {
        var topology = await ValidateScanTopologyAsync(token);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var pending = new List<string>(_configuration.BatchSize);
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanClient in _scanClients)
        foreach (var entry in scanClient.Nodes)
        {
            token.ThrowIfCancellationRequested();
            var identity = entry.Key;
            if (_configuration.Topology == CacheTopology.RedisCluster)
            {
                using var identityConnection = await entry.Value.GetAsync();
                identity = ClusterText(await identityConnection.Value.CallAsync("CLUSTER", new[] { "MYID" }));
            }
            if (!visitedNodes.Add(identity)) continue;
            var node = entry.Value;
            using var connection = await node.GetAsync();
            if (!string.Equals((await connection.Value.RoleAsync()).RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
            long cursor = 0;
            do
            {
                token.ThrowIfCancellationRequested();
                budget.ConsumePage();
                global::CSRedis.RedisScan<string> page =
                    await connection.Value.ScanAsync(cursor, pattern, _configuration.BatchSize);
                cursor = page.Cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physical in page.Items)
                {
                    CacheCommandProbe.RecordScanItem();
                    if (CacheKeyFormat.TryGetLogicalKey(hashContext, physical, out var logical))
                    {
                        token.ThrowIfCancellationRequested();
                        budget.Consume();
                        pending.Add(logical);
                        if (pending.Count >= _configuration.BatchSize)
                        {
                            await visitor(pending, token).ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            pending.Clear();
                        }
                    }
                }
            } while (cursor != 0);
            if (!string.Equals((await connection.Value.RoleAsync()).RoleName, "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
        }
        if (pending.Count > 0)
        {
            await visitor(pending, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        CacheClusterTopology.EnsureUnchanged(topology, await ValidateScanTopologyAsync(token));
    }

    /// <summary>
    /// 验证 Redis Cluster 的扫描拓扑。
    /// </summary>
    /// <returns>槽位归属快照；非 Cluster 拓扑返回 <see langword="null"/>。</returns>
    private string ValidateScanTopology()
    {
        if (_configuration.Topology != CacheTopology.RedisCluster) return null;
        var entries = _scanClients.SelectMany(scanClient => scanClient.Nodes)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        using var first = entries[0].Value.Get();
        var nodes = ClusterText(first.Value.Call("CLUSTER", new[] { "NODES" }));
        var snapshot = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var ids = new List<string>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            string view;
            if (index == 0)
                view = nodes;
            else
            {
                using var connection = entries[index].Value.Get();
                view = ClusterText(connection.Value.Call("CLUSTER", new[] { "NODES" }));
            }
            if (index != 0)
                CacheClusterTopology.EnsureSameView(snapshot, view);
            ids.Add(CacheClusterTopology.GetLocalMasterId(view));
        }
        CacheClusterTopology.Validate(nodes, ids.Distinct(StringComparer.Ordinal));
        return snapshot;
    }

    /// <summary>
    /// 异步验证 Redis Cluster 的扫描拓扑。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>槽位归属快照；非 Cluster 拓扑返回 <see langword="null"/>。</returns>
    private async Task<string> ValidateScanTopologyAsync(CancellationToken cancellationToken)
    {
        if (_configuration.Topology != CacheTopology.RedisCluster) return null;
        var entries = _scanClients.SelectMany(scanClient => scanClient.Nodes)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        string nodes;
        using (var first = await entries[0].Value.GetAsync())
            nodes = ClusterText(await first.Value.CallAsync("CLUSTER", new[] { "NODES" }));
        var snapshot = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var ids = new List<string>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string view;
            if (index == 0)
                view = nodes;
            else
            {
                using var connection = await entries[index].Value.GetAsync();
                view = ClusterText(await connection.Value.CallAsync("CLUSTER", new[] { "NODES" }));
            }
            if (index != 0)
                CacheClusterTopology.EnsureSameView(snapshot, view);
            ids.Add(CacheClusterTopology.GetLocalMasterId(view));
        }
        CacheClusterTopology.Validate(nodes, ids.Distinct(StringComparer.Ordinal));
        return snapshot;
    }

    /// <summary>
    /// 将 Redis Cluster 响应转换为文本。
    /// </summary>
    /// <param name="result">Redis 命令响应。</param>
    /// <returns>响应文本；空响应返回 <see langword="null"/>。</returns>
    private static string ClusterText(object result) => result is byte[] bytes
        ? Encoding.UTF8.GetString(bytes)
        : result?.ToString();

    /// <summary>
    /// 获取当前缓存实例的命名空间扫描模式。
    /// </summary>
    private string NamespacePattern => CacheKeyFormat.GetNamespacePattern(_configuration);

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix)) return new List<T>();
        var result = new List<T>();
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var physicalKey in ScanPhysicalPrefix(CacheKeyFormat.GetPrefixPattern(_configuration, prefix), prefix))
        {
            pending.Add(physicalKey);
            if (pending.Count < _configuration.BatchSize) continue;
            ReadScannedBatch(pending, result);
            pending.Clear();
        }
        ReadScannedBatch(pending, result);
        return result;
    }

    /// <summary>
    /// 读取一批扫描得到的物理键并追加到结果集合。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="result">接收读取值的结果集合。</param>
    private void ReadScannedBatch<T>(List<string> keys, List<T> result)
    {
        if (keys.Count == 0) return;
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            result.AddRange(ReadClusterValues<T>(keys));
        }
        else if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            var batch = new string[keys.Count];
            keys.CopyTo(batch, 0);
            result.AddRange(ReadShardedValuesAsync<T>(batch, CancellationToken.None).GetAwaiter().GetResult());
        }
        else
        {
            var batch = new string[keys.Count];
            keys.CopyTo(batch, 0);
            result.AddRange(ReadValidatedBatch<T>(ReadRawBatch(batch)));
        }
    }

    /// <summary>
    /// 按槽位分组读取 Redis Cluster 缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">待读取的物理键。</param>
    /// <returns>按输入顺序排列的缓存值。</returns>
    private T[] ReadClusterValues<T>(IList<string> physicalKeys)
    {
        var values = new T[physicalKeys.Count];
        foreach (var group in ClusterGroups(physicalKeys))
        {
            var indexes = group.Value;
            for (var offset = 0; offset < indexes.Count; offset += _configuration.BatchSize)
            {
                var count = Math.Min(_configuration.BatchSize, indexes.Count - offset);
                var batch = new string[count];
                for (var item = 0; item < count; item++)
                    batch[item] = physicalKeys[indexes[offset + item]];
                var read = ReadValidatedBatch<T>(ReadRawBatch(batch)).ToArray();
                if (read.Length != count)
                    throw new InvalidOperationException("Redis Cluster 批量读取返回数量与请求不一致。");
                for (var item = 0; item < count; item++)
                    values[indexes[offset + item]] = read[item];
            }
        }
        return values;
    }

    /// <inheritdoc />
    public async Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        var result = new List<T>();
        if (string.IsNullOrEmpty(prefix))
            return result;

        var physicalKeys = new string[_configuration.BatchSize];
        var physicalCount = 0;
        await foreach (var physicalKey in EnumerateMatchingPhysicalKeysAsync(prefix, cancellationToken))
        {
            physicalKeys[physicalCount++] = physicalKey;
            if (physicalCount < physicalKeys.Length)
                continue;
            result.AddRange(await ReadValuesBatchAsync<T>(physicalKeys, cancellationToken)
                .ConfigureAwait(false));
            Array.Clear(physicalKeys, 0, physicalCount);
            physicalCount = 0;
        }
        if (physicalCount > 0)
            result.AddRange(await ReadValuesBatchAsync<T>(CopyBatch(physicalKeys, physicalCount), cancellationToken)
                .ConfigureAwait(false));
        return result;
    }

    /// <summary>
    /// 异步读取一批扫描得到的物理键并追加到结果集合。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="result">接收读取值的结果集合。</param>
    /// <param name="token">取消令牌。</param>
    private async Task ReadScannedBatchAsync<T>(List<string> keys, List<T> result, CancellationToken token)
    {
        if (keys.Count == 0) return;
        token.ThrowIfCancellationRequested();
        if (_configuration.Topology != CacheTopology.RedisCluster)
        {
            var batch = new string[keys.Count];
            keys.CopyTo(batch, 0);
            result.AddRange(await _client.MGetAsync<T>(batch).ConfigureAwait(false));
            return;
        }
        for (var offset = 0; offset < keys.Count; offset += _configuration.MaxConcurrency)
        {
            token.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, keys.Count - offset);
            var tasks = new Task<T>[count];
            for (var index = 0; index < count; index++)
                tasks[index] = _client.GetAsync<T>(keys[offset + index]);
            result.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
        }
    }

    /// <inheritdoc />
    public bool TrySet<T>(CacheKey key, T value, CacheOptions options = null) { key.Validate(); return TrySet(key.Key, value, options); }
    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, CacheOptions options = null) => Write(key, value, Expiration(options), true);
    /// <inheritdoc />
    public Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    { key.Validate(); return TrySetAsync(key.Key, value, options, cancellationToken); }
    /// <inheritdoc />
    public Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
        => WriteAsync(key, value, Expiration(options), true, cancellationToken);

    /// <inheritdoc />
    public void Set<T>(CacheKey key, T value, CacheOptions options = null) { key.Validate(); Set(key.Key, value, options); }
    /// <inheritdoc />
    public void Set<T>(string key, T value, CacheOptions options = null) => Write(key, value, Expiration(options));
    /// <inheritdoc />
    public Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
    { key.Validate(); return SetAsync(key.Key, value, options, cancellationToken); }
    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null, CancellationToken cancellationToken = default)
        => await WriteAsync(key, value, Expiration(options), false, cancellationToken);

    /// <summary>
    /// 将缓存键字典转换为逻辑键字典。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">待转换的缓存项。</param>
    /// <returns>以逻辑键为键的缓存项字典。</returns>
    private static IDictionary<string, T> Items<T>(IDictionary<CacheKey, T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        return items.ToDictionary(item => { item.Key.Validate(); return item.Key.Key; }, item => item.Value);
    }
    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null) => SetAll(Items(items), options);
    /// <inheritdoc />
    public void SetAll<T>(IDictionary<string, T> items, CacheOptions options = null)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var expiration = Expiration(options);
        foreach (var item in items) Write(item.Key, item.Value, expiration);
    }
    /// <inheritdoc />
    public Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
        => SetAllAsync(Items(items), options, cancellationToken);
    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        var expiration = Expiration(options);
        var input = items.ToArray();
        for (var offset = 0; offset < input.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, input.Length - offset);
            var tasks = new Task[count];
            for (var index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = input[offset + index];
                tasks[index] = WriteAsync(item.Key, item.Value, expiration, false, cancellationToken);
            }
            await Task.WhenAll(tasks);
        }
    }

    /// <inheritdoc />
    public void Remove(CacheKey key) { key.Validate(); Remove(key.Key); }
    /// <inheritdoc />
    public void Remove(string key) => RemoveLogicalBatch(new[] { key });
    /// <inheritdoc />
    public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default) { key.Validate(); return RemoveAsync(key.Key, cancellationToken); }
    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await RemoveLogicalBatchAsync(new[] { key }, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys) => RemoveAll(Keys(keys));
    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));

        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;

            RemoveLogicalBatch(pending);
            pending.Clear();
        }

        RemoveLogicalBatch(pending);
    }
    /// <inheritdoc />
    public Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => RemoveAllAsync(Keys(keys), cancellationToken);
    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;

            await RemoveLogicalBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            pending.Clear();
        }

        await RemoveLogicalBatchAsync(pending, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 保存同一客户端的待删除物理键。
    /// </summary>
    private sealed class PhysicalDeleteGroup
    {
        /// <summary>
        /// 保存执行删除命令的客户端。
        /// </summary>
        internal readonly CSRedisClient Client;
        /// <summary>
        /// 保存该分组中的待删除物理键。
        /// </summary>
        internal readonly List<string> PhysicalKeys;

        /// <summary>
        /// 初始化一个 <see cref="PhysicalDeleteGroup"/> 类型的实例。
        /// </summary>
        /// <param name="client">执行删除命令的客户端。</param>
        internal PhysicalDeleteGroup(CSRedisClient client)
        {
            Client = client;
            PhysicalKeys = new List<string>();
        }
    }

    /// <summary>
    /// 保存一次批量物理键删除命令及其目标客户端。
    /// </summary>
    private sealed class PhysicalDeleteCommand
    {
        /// <summary>
        /// 保存执行删除命令的客户端。
        /// </summary>
        internal readonly CSRedisClient Client;
        /// <summary>
        /// 保存本次命令涉及的物理键。
        /// </summary>
        internal readonly IReadOnlyList<string> PhysicalKeys;

        /// <summary>
        /// 初始化一个 <see cref="PhysicalDeleteCommand"/> 类型的实例。
        /// </summary>
        /// <param name="client">执行删除命令的客户端。</param>
        /// <param name="physicalKeys">命令涉及的物理键。</param>
        internal PhysicalDeleteCommand(CSRedisClient client, IReadOnlyList<string> physicalKeys)
        {
            Client = client;
            PhysicalKeys = physicalKeys;
        }
    }

    /// <summary>
    /// 按 Redis 槽位或客户端分片分组物理键。
    /// </summary>
    /// <param name="physicalKeys">待分组的物理键。</param>
    /// <returns>物理键删除分组。</returns>
    private List<PhysicalDeleteGroup> GroupPhysicalDeleteKeys(IReadOnlyList<string> physicalKeys)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));

        // Cluster 的多键 DEL 必须同槽；数据键和租约键共享物理键 hash tag，因此只需按数据键槽位分组。
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            var groups = new Dictionary<int, PhysicalDeleteGroup>();
            for (var index = 0; index < physicalKeys.Count; index++)
            {
                var physicalKey = physicalKeys[index];
                var slot = CacheClusterTopology.GetHashSlot(physicalKey);
                if (!groups.TryGetValue(slot, out var group))
                    groups.Add(slot, group = new PhysicalDeleteGroup(_client));
                group.PhysicalKeys.Add(physicalKey);
            }
            return groups.Values.ToList();
        }

        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            var groups = new Dictionary<int, PhysicalDeleteGroup>();
            for (var index = 0; index < physicalKeys.Count; index++)
            {
                var physicalKey = physicalKeys[index];
                var nodeIndex = _shardNodeIndex(physicalKey);
                if (nodeIndex < 0 || nodeIndex >= _scanClients.Count)
                    throw new InvalidOperationException("Redis 分片路由返回无效节点索引。");
                if (!groups.TryGetValue(nodeIndex, out var group))
                    groups.Add(nodeIndex, group = new PhysicalDeleteGroup(_scanClients[nodeIndex]));
                group.PhysicalKeys.Add(physicalKey);
            }
            return groups.Values.ToList();
        }

        var single = new PhysicalDeleteGroup(_client);
        for (var index = 0; index < physicalKeys.Count; index++)
            single.PhysicalKeys.Add(physicalKeys[index]);
        return new List<PhysicalDeleteGroup> { single };
    }

    /// <summary>
    /// 生成同时包含数据键和租约键的删除参数。
    /// </summary>
    /// <param name="physicalKeys">数据物理键。</param>
    /// <returns>Redis 删除命令参数。</returns>
    private static string[] DeleteArguments(IReadOnlyList<string> physicalKeys)
    {
        var arguments = new string[physicalKeys.Count * 2];
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var physicalKey = physicalKeys[index];
            arguments[index * 2] = physicalKey;
            arguments[index * 2 + 1] = physicalKey + ":lease";
        }
        return arguments;
    }

    /// <summary>
    /// 按拓扑和批大小生成物理删除命令。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理键。</param>
    /// <returns>待执行的删除命令。</returns>
    private List<PhysicalDeleteCommand> GetPhysicalDeleteCommands(IReadOnlyList<string> physicalKeys)
    {
        var commands = new List<PhysicalDeleteCommand>();
        foreach (var group in GroupPhysicalDeleteKeys(physicalKeys))
        {
            foreach (var batch in CacheDeleteBatchBudget.Partition(group.PhysicalKeys, _configuration.BatchSize,
                         _configuration.Resources.MaxBatchCommandBytes))
                commands.Add(new PhysicalDeleteCommand(group.Client, batch));
        }
        return commands;
    }

    /// <summary>
    /// 同步删除一批物理键及其租约键。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理键。</param>
    private void RemovePhysicalBatch(IReadOnlyList<string> physicalKeys)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (physicalKeys.Count == 0)
            return;

        foreach (var command in GetPhysicalDeleteCommands(physicalKeys))
            command.Client.Del(DeleteArguments(command.PhysicalKeys));
    }

    /// <summary>
    /// 异步删除一批物理键及其租约键。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RemovePhysicalBatchAsync(IReadOnlyList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (physicalKeys.Count == 0)
            return;

        var commands = GetPhysicalDeleteCommands(physicalKeys);
        // 每轮最多派发 MaxConcurrency 个 DEL；取消只阻止后续派发，已发出的命令继续被观察。
        for (var offset = 0; offset < commands.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, commands.Count - offset);
            var tasks = new Task<long>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var command = commands[offset + dispatched];
                    var arguments = DeleteArguments(command.PhysicalKeys);
                    tasks[dispatched] = CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete,
                        () => command.Client.DelAsync(arguments));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// 同步删除一批逻辑键及其租约键。
    /// </summary>
    /// <param name="logicalKeys">待删除的逻辑键。</param>
    private void RemoveLogicalBatch(IReadOnlyList<string> logicalKeys)
    {
        if (logicalKeys == null)
            throw new ArgumentNullException(nameof(logicalKeys));
        if (logicalKeys.Count == 0)
            return;

        var physicalKeys = new string[logicalKeys.Count];
        for (var index = 0; index < logicalKeys.Count; index++)
            physicalKeys[index] = Key(logicalKeys[index]);
        RemovePhysicalBatch(physicalKeys);
    }

    /// <summary>
    /// 异步删除一批逻辑键及其租约键。
    /// </summary>
    /// <param name="logicalKeys">待删除的逻辑键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RemoveLogicalBatchAsync(IReadOnlyList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        if (logicalKeys == null)
            throw new ArgumentNullException(nameof(logicalKeys));
        if (logicalKeys.Count == 0)
            return;

        var physicalKeys = new string[logicalKeys.Count];
        for (var index = 0; index < logicalKeys.Count; index++)
            physicalKeys[index] = Key(logicalKeys[index]);
        await RemovePhysicalBatchAsync(physicalKeys, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按条件扫描并删除匹配的物理键。
    /// </summary>
    /// <param name="predicate">逻辑键匹配条件。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    private void RemoveMatching(Func<string, bool> predicate, string scanPattern = null)
    {
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in Scan(scanPattern ?? NamespacePattern))
        {
            if (!predicate(key))
                continue;
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;
            RemoveLogicalBatch(pending);
            pending.Clear();
        }
        RemoveLogicalBatch(pending);
    }

    /// <summary>
    /// 异步按条件扫描并删除匹配的物理键。
    /// </summary>
    /// <param name="predicate">逻辑键匹配条件。</param>
    /// <param name="token">取消令牌。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    private async Task RemoveMatchingAsync(Func<string, bool> predicate, CancellationToken token,
        string scanPattern = null)
    {
        await VisitAsync(scanPattern ?? NamespacePattern, async (keys, cancellationToken) =>
        {
            var matching = new List<string>(keys.Count);
            for (var index = 0; index < keys.Count; index++)
            {
                if (predicate(keys[index]))
                    matching.Add(keys[index]);
            }
            await RemoveLogicalBatchAsync(matching, cancellationToken).ConfigureAwait(false);
        }, token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空。", nameof(prefix));
        RemoveMatching(key => key.StartsWith(prefix, StringComparison.Ordinal),
            CacheKeyFormat.GetPrefixPattern(_configuration, prefix));
    }
    /// <inheritdoc />
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("前缀不能为空。", nameof(prefix));
        return RemoveMatchingAsync(key => key.StartsWith(prefix, StringComparison.Ordinal), cancellationToken,
            CacheKeyFormat.GetPrefixPattern(_configuration, prefix));
    }
    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("模式不能为空。", nameof(pattern));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            Remove(matcher.LiteralPrefix);
            return;
        }
        RemoveMatching(matcher.IsMatch, CacheKeyFormat.GetPatternScanPattern(_configuration, matcher));
    }
    /// <inheritdoc />
    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (string.IsNullOrWhiteSpace(pattern)) throw new ArgumentException("模式不能为空。", nameof(pattern));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RemoveAsync(matcher.LiteralPrefix, cancellationToken);
        }
        return RemoveMatchingAsync(matcher.IsMatch, cancellationToken,
            CacheKeyFormat.GetPatternScanPattern(_configuration, matcher));
    }
    /// <inheritdoc />
    public void Clear() => RemoveMatching(_ => true);
    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default) => RemoveMatchingAsync(_ => true, cancellationToken);
}
