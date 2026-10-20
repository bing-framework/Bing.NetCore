using Bing.Extensions;
using Bing.Helpers;
using FreeRedis;

namespace Bing.Caching.FreeRedis;

/// <summary>
/// FreeRedis 缓存管理器。
/// </summary>
// ReSharper disable once InconsistentNaming
public partial class FreeRedisCacheManager : IRedisCache
{
    /// <summary>
    /// 用于读写缓存数据的 Redis 客户端。
    /// </summary>
    private readonly RedisClient _client;
    /// <summary>
    /// 当前缓存实例的配置。
    /// </summary>
    private readonly CacheConfiguration _configuration;
    /// <summary>
    /// 用于扫描缓存键的主节点或分片客户端。
    /// </summary>
    private readonly IReadOnlyList<RedisClient> _scanClients;

    /// <summary>
    /// 初始化一个 <see cref="FreeRedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="client">用于读写缓存的 Redis 客户端。</param>
    /// <param name="configuration">缓存实例配置。</param>
    /// <param name="scanClients">集群或分片的主节点扫描客户端。</param>
    /// <remarks>客户端不得配置隐藏前缀；集群和分片场景需提供完整的主节点集合。</remarks>
    public FreeRedisCacheManager(RedisClient client, CacheConfiguration configuration,
        IReadOnlyList<RedisClient> scanClients = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (configuration.Topology != CacheTopology.SingleNode && (scanClients == null || scanClients.Count == 0))
            throw new ArgumentException("集群或分片必须提供全部主节点扫描客户端。", nameof(scanClients));
        _scanClients = scanClients ?? new[] { client };
        if (configuration.Topology == CacheTopology.ClientSharded &&
            _scanClients.Count != configuration.ExpectedShardCount)
            throw new ArgumentException("扫描客户端数量与预期分片数不一致。", nameof(scanClients));
        foreach (var scanClient in _scanClients)
        {
            if (scanClient == null)
                throw new ArgumentException("扫描客户端不能为空。", nameof(scanClients));
            EnsureNoClientPrefix(scanClient);
        }
        if (configuration.Topology == CacheTopology.ClientSharded)
            CacheShardTopology.Validate(_scanClients.Select(scanClient => scanClient.Info("server")),
                configuration.ExpectedShardCount.Value);
        EnsureNoClientPrefix(client);
        if (client.Serialize == null || client.Deserialize == null)
            throw new ArgumentException("FreeRedis 必须设置了序列化/反序列化 client.Serialize/Deserialize");
    }

    /// <summary>
    /// 将逻辑缓存键转换为当前命名空间下的物理键。
    /// </summary>
    /// <param name="key">逻辑缓存键。</param>
    /// <returns>命名空间隔离后的物理键。</returns>
    private string PhysicalKey(string key) => CacheKeyFormat.GetPhysicalKey(_configuration, key);

    /// <summary>
    /// 验证 Redis 客户端未配置隐藏前缀。
    /// </summary>
    /// <param name="client">待验证的 Redis 客户端。</param>
    private static void EnsureNoClientPrefix(RedisClient client)
    {
        // FreeRedis 1.1.5 未公开 Prefix；使用其内部只读属性防止隐藏前缀绕过隔离。
        var property = typeof(RedisClient).GetProperty("Prefix", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (property == null || property.PropertyType != typeof(string))
            throw new NotSupportedException("无法验证 FreeRedis 客户端前缀配置。");
        if (!string.IsNullOrEmpty((string)property.GetValue(client)))
            throw new ArgumentException("FreeRedis 客户端不能配置 prefix；命名空间由 CacheConfiguration 管理。", nameof(client));
    }

    /// <summary>
    /// 根据物理键选择对应的 Redis 客户端。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>负责该键读写的 Redis 客户端。</returns>
    private RedisClient ClientForPhysicalKey(string physicalKey)
    {
        if (_configuration.Topology != CacheTopology.ClientSharded)
            return _client;
        var start = physicalKey.IndexOf('{') + 1;
        var shard = Convert.ToUInt32(physicalKey.Substring(start, 8), 16) % (uint)_scanClients.Count;
        return _scanClients[(int)shard];
    }

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
        var physicalKey = PhysicalKey(key);
        return ClientForPhysicalKey(physicalKey).Exists(physicalKey);
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
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        return await ClientForPhysicalKey(physicalKey).ExistsAsync(physicalKey);
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
        var physicalKey = PhysicalKey(key);
        return ClientForPhysicalKey(physicalKey).Get<T>(physicalKey);
    }

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, dataRetriever, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null)
    {
        if (dataRetriever == null)
            throw new ArgumentNullException(nameof(dataRetriever));
        return GetOrCreate(key, dataRetriever, options);
    }

    /// <summary>
    /// 获取本次写入采用的有效期。
    /// </summary>
    /// <param name="options">缓存选项。</param>
    /// <returns>经过配置规范化的缓存有效期。</returns>
    private TimeSpan GetExpiration(CacheOptions options)
    {
        return _configuration.GetExpiration(options);
    }

    #endregion

    #region GetAsync

    /// <inheritdoc />
    public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        var client = ClientForPhysicalKey(physicalKey);
        var result = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => client.GetAsync(physicalKey))
            .ConfigureAwait(false);
        if (result != null)
        {
            var value = client.Deserialize(result, type);
            return value;
        }
        return null;
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
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        return await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => ClientForPhysicalKey(physicalKey).GetAsync<T>(physicalKey))
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (dataRetriever == null)
            throw new ArgumentNullException(nameof(dataRetriever));
        return await GetOrCreateAsync(key, _ => dataRetriever(), options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync(key, dataRetriever ?? throw new ArgumentNullException(nameof(dataRetriever)), options, cancellationToken);

    #endregion

    #region GetAll

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys) => GetAll<T>(ToKeys(keys));

    /// <summary>
    /// 转换为缓存键字符串集合
    /// </summary>
    /// <param name="keys">缓存键集合。</param>
    /// <returns>已验证并转换为字符串的缓存键集合。</returns>
    private IEnumerable<string> ToKeys(IEnumerable<CacheKey> keys)
    {
        keys.CheckNull(nameof(keys));
        var cacheKeys = keys.ToList();
        cacheKeys.ForEach(key => key.Validate());
        return cacheKeys.Select(key => key.Key);
    }

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var physicalKeys = keys.Select(PhysicalKey).ToArray();
        var values = new T[physicalKeys.Length];
        if (_configuration.Topology == CacheTopology.RedisCluster)
            return new List<T>(ReadClusterValues<T>(physicalKeys));

        if (_configuration.Topology == CacheTopology.SingleNode)
        {
            for (var offset = 0; offset < physicalKeys.Length; offset += _configuration.BatchSize)
            {
                var count = Math.Min(_configuration.BatchSize, physicalKeys.Length - offset);
                var batch = new string[count];
                Array.Copy(physicalKeys, offset, batch, 0, count);
                var read = ReadPhysicalBatch<T>(_client, batch);
                Array.Copy(read, 0, values, offset, count);
            }
            return new List<T>(values);
        }

        // 同步 API 复用客户端分片的有界异步路由，避免逐分片同步 MGET；辅助方法使用 ConfigureAwait(false)，不会依赖调用方同步上下文。
        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            for (var offset = 0; offset < physicalKeys.Length; offset += _configuration.BatchSize)
            {
                var count = Math.Min(_configuration.BatchSize, physicalKeys.Length - offset);
                var batch = new string[count];
                Array.Copy(physicalKeys, offset, batch, 0, count);
                var read = ReadValuesBatchAsync<T>(batch, CancellationToken.None).GetAwaiter().GetResult();
                Array.Copy(read, 0, values, offset, count);
            }
            return new List<T>(values);
        }

        foreach (var group in GroupIndexes(physicalKeys))
        {
            var indexes = group.Value;
            for (var offset = 0; offset < indexes.Count; offset += _configuration.BatchSize)
            {
                var count = Math.Min(_configuration.BatchSize, indexes.Count - offset);
                var batch = new string[count];
                for (var item = 0; item < count; item++)
                    batch[item] = physicalKeys[indexes[offset + item]];
                var read = ReadPhysicalBatch<T>(group.Key, batch);
                for (var item = 0; item < count; item++)
                    values[indexes[offset + item]] = read[item];
            }
        }
        return new List<T>(values);
    }

    /// <summary>
    /// 按物理键路由客户端并保留输入索引。
    /// </summary>
    /// <param name="physicalKeys">物理键数组。</param>
    /// <returns>客户端对应的输入索引集合。</returns>
    private Dictionary<RedisClient, List<int>> GroupIndexes(string[] physicalKeys)
    {
        var groups = new Dictionary<RedisClient, List<int>>();
        for (var index = 0; index < physicalKeys.Length; index++)
        {
            var client = ClientForPhysicalKey(physicalKeys[index]);
            if (!groups.TryGetValue(client, out var indexes))
                groups.Add(client, indexes = new List<int>());
            indexes.Add(index);
        }
        return groups;
    }

    /// <summary>
    /// 按 Redis Cluster 槽位分组读取缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键列表。</param>
    /// <returns>与输入顺序一致的缓存值。</returns>
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
                var read = ReadPhysicalBatch<T>(_client, batch);
                if (read.Length != count)
                    throw new InvalidOperationException("Redis Cluster 批量读取返回数量与请求不一致。");
                for (var item = 0; item < count; item++)
                    values[indexes[offset + item]] = read[item];
            }
        }
        return values;
    }

    #endregion

    #region GetAllAsync

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => await GetAllAsync<T>(ToKeys(keys), cancellationToken);

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
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

    #endregion

    #region GetByPrefix

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix))
            return new List<T>();
        var values = new List<T>();
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in EnumerateKeys(key => key.StartsWith(prefix, StringComparison.Ordinal),
                     CacheKeyFormat.GetPrefixPattern(_configuration, prefix)))
        {
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize) continue;
            ReadScannedBatch(pending, values);
            pending.Clear();
        }
        ReadScannedBatch(pending, values);
        return values;
    }

    /// <summary>
    /// 读取一批扫描得到的物理键并追加缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="values">接收读取结果的集合。</param>
    private void ReadScannedBatch<T>(List<string> keys, List<T> values)
    {
        if (keys.Count == 0) return;
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            values.AddRange(ReadClusterValues<T>(keys));
            return;
        }

        if (_configuration.Topology == CacheTopology.SingleNode)
        {
            values.AddRange(ReadPhysicalBatch<T>(_client, keys.ToArray()));
            return;
        }

        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            values.AddRange(ReadValuesBatchAsync<T>(keys.ToArray(), CancellationToken.None)
                .GetAwaiter().GetResult());
            return;
        }

        var groups = new Dictionary<RedisClient, List<string>>();
        for (var index = 0; index < keys.Count; index++)
        {
            var client = ClientForPhysicalKey(keys[index]);
            if (!groups.TryGetValue(client, out var group))
                groups.Add(client, group = new List<string>());
            group.Add(keys[index]);
        }
        foreach (var group in groups)
            values.AddRange(ReadPhysicalBatch<T>(group.Key, group.Value.ToArray()));
    }

    /// <summary>
    /// 扫描当前命名空间并枚举符合条件的物理键。
    /// </summary>
    /// <param name="matches">逻辑键匹配条件。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    /// <returns>匹配项对应的物理键序列。</returns>
    private IEnumerable<string> EnumerateKeys(Func<string, bool> matches, string scanPattern = null)
    {
        var topology = ValidateScanTopology();
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        scanPattern ??= CacheKeyFormat.GetNamespacePattern(_configuration);
        foreach (var scanClient in _scanClients)
        {
            long cursor = 0;
            do
            {
                budget.ConsumePage();
                var page = scanClient.Scan(cursor, scanPattern, _configuration.BatchSize, "");
                cursor = page.cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.items)
                {
                    CacheCommandProbe.RecordScanItem();
                    if (CacheKeyFormat.TryGetLogicalKey(hashContext, physicalKey, out var logicalKey))
                    {
                        budget.Consume();
                        if (matches(logicalKey))
                            yield return physicalKey;
                    }
                }
            } while (cursor != 0);
        }
        CacheClusterTopology.EnsureUnchanged(topology, ValidateScanTopology());
    }

    /// <summary>
    /// 异步扫描当前命名空间并分批处理匹配的物理键。
    /// </summary>
    /// <param name="matches">逻辑键匹配条件。</param>
    /// <param name="action">处理每批物理键的委托。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="scanPattern">可选的物理键扫描模式。</param>
    private async Task ForEachKeyAsync(Func<string, bool> matches,
        Func<IReadOnlyList<string>, CancellationToken, Task> action,
        CancellationToken cancellationToken, string scanPattern = null)
    {
        var topology = await ValidateScanTopologyAsync(cancellationToken);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        scanPattern ??= CacheKeyFormat.GetNamespacePattern(_configuration);
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var scanClient in _scanClients)
        {
            long cursor = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.ConsumePage();
                var page = await scanClient.ScanAsync(cursor, scanPattern, _configuration.BatchSize, "");
                cursor = page.cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.items)
                {
                    CacheCommandProbe.RecordScanItem();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CacheKeyFormat.TryGetLogicalKey(hashContext, physicalKey, out var logicalKey))
                    {
                        budget.Consume();
                        if (matches(logicalKey))
                        {
                            pending.Add(physicalKey);
                            if (pending.Count >= _configuration.BatchSize)
                            {
                                await action(pending, cancellationToken);
                                cancellationToken.ThrowIfCancellationRequested();
                                pending.Clear();
                            }
                        }
                    }
                }
            } while (cursor != 0);
        }
        if (pending.Count > 0)
        {
            await action(pending, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        CacheClusterTopology.EnsureUnchanged(topology, await ValidateScanTopologyAsync(cancellationToken));
    }

    /// <summary>
    /// 校验同步扫描期间的 Cluster 拓扑视图。
    /// </summary>
    /// <returns>拓扑快照；非 Cluster 拓扑返回 <see langword="null"/>。</returns>
    private string ValidateScanTopology()
    {
        if (_configuration.Topology != CacheTopology.RedisCluster) return null;
        var nodes = _scanClients[0].Call(new CommandPacket("CLUSTER", "NODES")) as string;
        var snapshot = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var ids = new List<string>(_scanClients.Count);
        for (var index = 0; index < _scanClients.Count; index++)
        {
            var view = index == 0 ? nodes :
                _scanClients[index].Call(new CommandPacket("CLUSTER", "NODES")) as string;
            if (index != 0)
                CacheClusterTopology.EnsureSameView(snapshot, view);
            ids.Add(CacheClusterTopology.GetLocalMasterId(view));
        }
        CacheClusterTopology.Validate(nodes, ids);
        return snapshot;
    }

    /// <summary>
    /// 异步校验扫描期间的 Cluster 拓扑视图。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>拓扑快照；非 Cluster 拓扑返回 <see langword="null"/>。</returns>
    private async Task<string> ValidateScanTopologyAsync(CancellationToken cancellationToken)
    {
        if (_configuration.Topology != CacheTopology.RedisCluster) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var nodes = await _scanClients[0].CallAsync(new CommandPacket("CLUSTER", "NODES")) as string;
        var snapshot = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var ids = new List<string>(_scanClients.Count);
        for (var index = 0; index < _scanClients.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var view = index == 0 ? nodes :
                await _scanClients[index].CallAsync(new CommandPacket("CLUSTER", "NODES")) as string;
            if (index != 0)
                CacheClusterTopology.EnsureSameView(snapshot, view);
            ids.Add(CacheClusterTopology.GetLocalMasterId(view));
        }
        CacheClusterTopology.Validate(nodes, ids);
        return snapshot;
    }

    #endregion

    #region GetByPrefixAsync

    /// <inheritdoc />
    public async Task<List<T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        var values = new List<T>();
        if (string.IsNullOrEmpty(prefix))
            return values;

        var physicalKeys = new string[_configuration.BatchSize];
        var physicalCount = 0;
        await foreach (var physicalKey in EnumerateMatchingPhysicalKeysAsync(prefix, cancellationToken))
        {
            physicalKeys[physicalCount++] = physicalKey;
            if (physicalCount < physicalKeys.Length)
                continue;
            values.AddRange(await ReadValuesBatchAsync<T>(physicalKeys, cancellationToken)
                .ConfigureAwait(false));
            Array.Clear(physicalKeys, 0, physicalCount);
            physicalCount = 0;
        }
        if (physicalCount > 0)
            values.AddRange(await ReadValuesBatchAsync<T>(CopyBatch(physicalKeys, physicalCount), cancellationToken)
                .ConfigureAwait(false));
        return values;
    }

    /// <summary>
    /// 异步读取一批扫描得到的物理键并追加缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="values">接收读取结果的集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ReadScannedBatchAsync<T>(List<string> keys, List<T> values,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            for (var offset = 0; offset < keys.Count; offset += _configuration.MaxConcurrency)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(_configuration.MaxConcurrency, keys.Count - offset);
                var tasks = new Task<T>[count];
                for (var index = 0; index < count; index++)
                    tasks[index] = _client.GetAsync<T>(keys[offset + index]);
                values.AddRange(await Task.WhenAll(tasks).ConfigureAwait(false));
            }
            return;
        }
        var groups = new Dictionary<RedisClient, List<string>>();
        for (var index = 0; index < keys.Count; index++)
        {
            var client = ClientForPhysicalKey(keys[index]);
            if (!groups.TryGetValue(client, out var group))
                groups.Add(client, group = new List<string>());
            group.Add(keys[index]);
        }
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values.AddRange(await group.Key.MGetAsync<T>(group.Value.ToArray()).ConfigureAwait(false));
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
        var physicalKey = PhysicalKey(key);
        return WritePhysical(physicalKey, value, GetExpiration(options), true);
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
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        return await WritePhysicalAsync(physicalKey, value, GetExpiration(options), true);
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
        var physicalKey = PhysicalKey(key);
        WritePhysical(physicalKey, value, GetExpiration(options));
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
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        await WritePhysicalAsync(physicalKey, value, GetExpiration(options));
    }

    #endregion

    #region SetAll

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null) => SetAll(ToItems(items), options);

    /// <summary>
    /// 转换为缓存项字典
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
        foreach (var item in items)
        {
            var physicalKey = PhysicalKey(item.Key);
            WritePhysical(physicalKey, item.Value, expiration);
        }
    }

    #endregion

    #region SetAllAsync

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null, CancellationToken cancellationToken = default) => await SetAllAsync(ToItems(items), options, cancellationToken);

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        var expiration = GetExpiration(options);
        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            await SetAllClientShardedAsync(items, expiration, cancellationToken).ConfigureAwait(false);
            return;
        }

        var pending = new List<Task>(_configuration.MaxConcurrency);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var physicalKey = PhysicalKey(item.Key);
            pending.Add(WritePhysicalAsync(physicalKey, item.Value, expiration));
            if (pending.Count == _configuration.MaxConcurrency)
            {
                await Task.WhenAll(pending);
                pending.Clear();
            }
        }
        if (pending.Count > 0)
            await Task.WhenAll(pending);
    }

    /// <summary>
    /// 按路由客户端分组异步写入缓存项。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">逻辑键和值的集合。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <remarks>同一客户端上的写入保持串行，不同客户端按配置并发执行。</remarks>
    private async Task SetAllClientShardedAsync<T>(IEnumerable<KeyValuePair<string, T>> items,
        TimeSpan expiration, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<RedisClient, List<KeyValuePair<string, T>>>();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var physicalKey = PhysicalKey(item.Key);
            var client = ClientForPhysicalKey(physicalKey);
            if (!groups.TryGetValue(client, out var group))
                groups.Add(client, group = new List<KeyValuePair<string, T>>());
            group.Add(new KeyValuePair<string, T>(physicalKey, item.Value));
        }

        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task[windowCount];
            var dispatched = 0;
            try
            {
                for (; dispatched < windowCount; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks[dispatched] = WriteClientGroupAsync(grouped[offset + dispatched].Value,
                        expiration, cancellationToken);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch
            {
                for (var index = 0; index < dispatched; index++)
                {
                    try
                    {
                        await tasks[index].ConfigureAwait(false);
                    }
                    catch
                    {
                        // 保留首个派发阶段或 WhenAll 抛出的异常，同时观察其余任务。
                    }
                }
                throw;
            }
        }
    }

    /// <summary>
    /// 按输入顺序写入路由到同一客户端的缓存项。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">已转换为物理键的缓存项。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task WriteClientGroupAsync<T>(IReadOnlyList<KeyValuePair<string, T>> items,
        TimeSpan expiration, CancellationToken cancellationToken)
    {
        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            await WritePhysicalAsync(item.Key, item.Value, expiration).ConfigureAwait(false);
        }
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
        var physicalKey = PhysicalKey(key);
        RemovePhysicalBatch(new[] { physicalKey });
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
        cancellationToken.ThrowIfCancellationRequested();
        var physicalKey = PhysicalKey(key);
        await RemovePhysicalBatchAsync(new[] { physicalKey }, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region RemoveAll

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys) => RemoveAll(ToKeys(keys));

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            pending.Add(PhysicalKey(key));
            if (pending.Count < _configuration.BatchSize)
                continue;

            RemovePhysicalBatch(pending);
            pending.Clear();
        }

        RemovePhysicalBatch(pending);
    }

    #endregion

    #region RemoveAllAsync

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<CacheKey> keys, CancellationToken cancellationToken = default) => await RemoveAllAsync(ToKeys(keys), cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pending.Add(PhysicalKey(key));
            if (pending.Count < _configuration.BatchSize)
                continue;

            await RemovePhysicalBatchAsync(pending, cancellationToken).ConfigureAwait(false);
            pending.Clear();
        }

        await RemovePhysicalBatchAsync(pending, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region RemoveByPrefix

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        Check.NotNullOrWhiteSpace(prefix, nameof(prefix));
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in EnumerateKeys(key => key.StartsWith(prefix, StringComparison.Ordinal),
                     CacheKeyFormat.GetPrefixPattern(_configuration, prefix)))
        {
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;
            RemovePhysicalBatch(pending);
            pending.Clear();
        }
        RemovePhysicalBatch(pending);
    }

    #endregion

    #region RemoveByPrefixAsync

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        Check.NotNullOrWhiteSpace(prefix, nameof(prefix));
        await ForEachKeyAsync(key => key.StartsWith(prefix, StringComparison.Ordinal),
            async (physicalKeys, token) => await RemovePhysicalBatchAsync(physicalKeys, token), cancellationToken,
            CacheKeyFormat.GetPrefixPattern(_configuration, prefix));
    }

    #endregion

    #region RemoveByPattern

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        Check.NotNullOrWhiteSpace(pattern, nameof(pattern));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            Remove(matcher.LiteralPrefix);
            return;
        }
        var scanPattern = CacheKeyFormat.GetPatternScanPattern(_configuration, matcher);
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in EnumerateKeys(logicalKey => matcher.IsMatch(logicalKey), scanPattern))
        {
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;
            RemovePhysicalBatch(pending);
            pending.Clear();
        }
        RemovePhysicalBatch(pending);
    }

    #endregion

    #region RemoveByPatternAsync

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        Check.NotNullOrWhiteSpace(pattern, nameof(pattern));
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            await RemoveAsync(matcher.LiteralPrefix, cancellationToken);
            return;
        }
        var scanPattern = CacheKeyFormat.GetPatternScanPattern(_configuration, matcher);
        await ForEachKeyAsync(key => matcher.IsMatch(key),
            async (physicalKeys, token) => await RemovePhysicalBatchAsync(physicalKeys, token), cancellationToken,
            scanPattern);
    }

    #endregion

    #region Clear

    /// <inheritdoc />
    public void Clear()
    {
        var pending = new List<string>(_configuration.BatchSize);
        foreach (var key in EnumerateKeys(_ => true))
        {
            pending.Add(key);
            if (pending.Count < _configuration.BatchSize)
                continue;
            RemovePhysicalBatch(pending);
            pending.Clear();
        }
        RemovePhysicalBatch(pending);
    }

    #endregion

    #region ClearAsync

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await ForEachKeyAsync(_ => true,
            async (physicalKeys, token) => await RemovePhysicalBatchAsync(physicalKeys, token), cancellationToken);
    }

    #endregion
}
