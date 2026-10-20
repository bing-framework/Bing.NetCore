using Bing.Caching;
using EasyCaching.Core;
using StackExchange.Redis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Bing.EasyCaching;

/// <summary>
/// 由多个独立 Redis 实例组成的客户端分片缓存适配器。
/// </summary>
public sealed class ShardedRedisCacheManager : IRedisCache
{
    /// <summary>
    /// 当前分片缓存使用的配置。
    /// </summary>
    private readonly CacheConfiguration _configuration;

    /// <summary>
    /// 各分片对应的 Redis 数据库。
    /// </summary>
    private readonly IReadOnlyList<IDatabase> _databases;

    /// <summary>
    /// 各分片对应的缓存管理器。
    /// </summary>
    private readonly IReadOnlyList<CacheManager> _managers;

    /// <summary>
    /// 当前适配器包含的分片数量。
    /// </summary>
    private readonly int _shardCount;

    /// <summary>
    /// 构造时校验通过的分片身份标识。
    /// </summary>
    private readonly string[] _expectedIds;

    /// <summary>
    /// 初始化一个 <see cref="ShardedRedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="providers">按稳定顺序排列的独立 Redis Provider。</param>
    /// <param name="configuration">客户端分片配置。</param>
    public ShardedRedisCacheManager(IReadOnlyList<IEasyCachingProvider> providers,
        CacheConfiguration configuration)
        : this(BuildShards(providers, configuration), configuration)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="ShardedRedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="parts">已经构造并校验的分片组件。</param>
    /// <param name="configuration">客户端分片配置。</param>
    private ShardedRedisCacheManager(ShardParts parts, CacheConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (parts == null)
            throw new ArgumentNullException(nameof(parts));
        _databases = parts.Databases;
        _managers = parts.Managers;
        _shardCount = _managers.Count;
        ValidateConfiguration(configuration, _shardCount);
        _expectedIds = ValidateShardIdentities();
    }

    /// <summary>
    /// 初始化一个 <see cref="ShardedRedisCacheManager"/> 类型的实例。
    /// </summary>
    /// <param name="shards">按稳定顺序排列的分片缓存管理器。</param>
    /// <param name="databases">按相同顺序排列的 Redis 数据库。</param>
    /// <param name="configuration">客户端分片配置。</param>
    /// <remarks>此重载还为各 Redis 分片配置本地 Hybrid Provider。</remarks>
    internal ShardedRedisCacheManager(IReadOnlyList<CacheManager> shards,
        IReadOnlyList<IDatabase> databases, CacheConfiguration configuration)
        : this(new ShardParts(shards, databases), configuration)
    {
    }

    /// <summary>
    /// 根据 Provider 创建分片管理器和数据库列表。
    /// </summary>
    /// <param name="providers">按稳定顺序排列的 Redis Provider。</param>
    /// <param name="configuration">客户端分片配置。</param>
    /// <returns>按相同顺序排列的分片组件。</returns>
    private static ShardParts BuildShards(IReadOnlyList<IEasyCachingProvider> providers,
        CacheConfiguration configuration)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        ValidateConfiguration(configuration, providers.Count);
        var databases = new IDatabase[providers.Count];
        var managers = new CacheManager[providers.Count];
        var innerConfiguration = new CacheConfiguration(configuration.Namespace, configuration.ProviderName,
            configuration.DefaultExpiration, configuration.BatchSize, configuration.MaxConcurrency,
            configuration.Coordination, CacheTopology.SingleNode, resources: configuration.Resources)
            .WithInputLimits(configuration.InputLimits)
            .WithSyncCleanupMode(configuration.SyncCleanupMode);
        for (var index = 0; index < providers.Count; index++)
        {
            var provider = providers[index] ??
                throw new ArgumentException("Redis 分片 Provider 不能为空。", nameof(providers));
            databases[index] = provider.Database as IDatabase ??
                throw new NotSupportedException("客户端分片需要 EasyCaching Redis Provider 提供 StackExchange.Redis 数据库。");
            managers[index] = new CacheManager(provider, innerConfiguration);
            managers[index].SetCoordinator(new RedisLeaseCoordinator(provider, innerConfiguration));
        }
        return new ShardParts(managers, databases);
    }

    /// <summary>
    /// 校验客户端分片配置与 Provider 数量。
    /// </summary>
    /// <param name="configuration">待校验的缓存配置。</param>
    /// <param name="count">当前 Provider 数量。</param>
    private static void ValidateConfiguration(CacheConfiguration configuration, int count)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (configuration.Topology != CacheTopology.ClientSharded)
            throw new ArgumentException("客户端分片适配器要求 ClientSharded 拓扑。", nameof(configuration));
        if (!configuration.ExpectedShardCount.HasValue ||
            count != configuration.ExpectedShardCount.Value)
            throw new ArgumentException("Provider 数量与预期分片数不一致。", nameof(count));
        if (count == 0)
            throw new ArgumentException("至少需要一个 Redis 分片。", nameof(count));
    }

    /// <summary>
    /// 保存分片管理器及其对应的数据库。
    /// </summary>
    private sealed class ShardParts
    {
        /// <summary>
        /// 初始化一个 <see cref="ShardParts"/> 类型的实例。
        /// </summary>
        /// <param name="managers">按稳定顺序排列的分片管理器。</param>
        /// <param name="databases">按相同顺序排列的 Redis 数据库。</param>
        internal ShardParts(IReadOnlyList<CacheManager> managers, IReadOnlyList<IDatabase> databases)
        {
            if (managers == null)
                throw new ArgumentNullException(nameof(managers));
            if (databases == null)
                throw new ArgumentNullException(nameof(databases));
            if (managers.Count != databases.Count)
                throw new ArgumentException("分片管理器与数据库数量不一致。", nameof(databases));
            if (managers.Any(manager => manager == null))
                throw new ArgumentException("分片管理器不能为空。", nameof(managers));
            if (databases.Any(database => database == null))
                throw new ArgumentException("分片数据库不能为空。", nameof(databases));
            Managers = managers.ToArray();
            Databases = databases.ToArray();
        }

        /// <summary>
        /// 获取分片管理器列表。
        /// </summary>
        internal IReadOnlyList<CacheManager> Managers { get; }

        /// <summary>
        /// 获取与管理器对应的 Redis 数据库列表。
        /// </summary>
        internal IReadOnlyList<IDatabase> Databases { get; }
    }

    #region Single-key operations

    /// <inheritdoc />
    public bool Exists(CacheKey key)
    {
        key.Validate();
        return Exists(key.Key);
    }

    /// <inheritdoc />
    public bool Exists(string key) => ExecuteForKey(key, manager => manager.Exists(key));

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return await ExistsAsync(key.Key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key, manager => manager.ExistsAsync(key, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public T Get<T>(CacheKey key)
    {
        key.Validate();
        return Get<T>(key.Key);
    }

    /// <inheritdoc />
    public T Get<T>(string key) => ExecuteForKey(key, manager => manager.Get<T>(key));

    /// <inheritdoc />
    public T Get<T>(CacheKey key, Func<T> dataRetriever, CacheOptions options = null)
    {
        key.Validate();
        return Get(key.Key, dataRetriever, options);
    }

    /// <inheritdoc />
    public T Get<T>(string key, Func<T> dataRetriever, CacheOptions options = null) =>
        ExecuteForKey(key, manager => manager.Get(key, dataRetriever, options));

    /// <inheritdoc />
    public bool TrySet<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        return TrySet(key.Key, value, options);
    }

    /// <inheritdoc />
    public bool TrySet<T>(string key, T value, CacheOptions options = null) =>
        ExecuteForKey(key, manager => manager.TrySet(key, value, options));

    /// <inheritdoc />
    public void Set<T>(CacheKey key, T value, CacheOptions options = null)
    {
        key.Validate();
        Set(key.Key, value, options);
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, CacheOptions options = null) =>
        ExecuteForKey(key, manager => manager.Set(key, value, options));

    /// <inheritdoc />
    public void Remove(CacheKey key)
    {
        key.Validate();
        Remove(key.Key);
    }

    /// <inheritdoc />
    public void Remove(string key) => ExecuteForKey(key, manager => manager.Remove(key));

    #endregion

    #region Single-key asynchronous operations

    /// <inheritdoc />
    public async Task<object> GetAsync(string key, Type type, CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key, manager => manager.GetAsync(key, type, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return GetAsync<T>(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key, manager => manager.GetAsync<T>(key, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key,
            manager => manager.GetAsync(key, dataRetriever, options, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<T> GetAsync<T>(CacheKey key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return GetAsync(key.Key, dataRetriever, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetAsync<T>(string key, Func<CancellationToken, Task<T>> dataRetriever,
        CacheOptions options = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key,
            manager => manager.GetAsync(key, dataRetriever, options, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> TrySetAsync<T>(CacheKey key, T value, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        key.Validate();
        return TrySetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TrySetAsync<T>(string key, T value, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteForKeyAsync(key,
            manager => manager.TrySetAsync(key, value, options, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(CacheKey key, T value, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        key.Validate();
        return SetAsync(key.Key, value, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteForKeyAsync(key,
            manager => manager.SetAsync(key, value, options, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        return RemoveAsync(key.Key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await ExecuteForKeyAsync(key, manager => manager.RemoveAsync(key, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Batch reads

    /// <inheritdoc />
    public List<T> GetAll<T>(IEnumerable<CacheKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        return GetAll<T>(ProjectKeys(keys));
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
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            result.AddRange(ReadSyncBatch<T>(batch));
            batch.Clear();
        }
        if (batch.Count > 0)
            result.AddRange(ReadSyncBatch<T>(batch));
        return result;
    }

    /// <summary>
    /// 按分片读取一批缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">待读取的逻辑键，顺序和重复项均予以保留。</param>
    /// <returns>与输入顺序对应的缓存值数组。</returns>
    private T[] ReadSyncBatch<T>(IList<string> logicalKeys)
    {
        var result = new T[logicalKeys.Count];
        if (logicalKeys.Count > 0)
            ValidateShardIdentities(_expectedIds);
        var groups = GroupKeys(logicalKeys);
        foreach (var group in groups)
        {
            var values = _managers[group.Shard].GetAll<T>(group.Keys);
            if (values.Count != group.Indices.Count)
                throw new InvalidOperationException("Redis 分片批量读取返回数量与请求不一致。");
            for (var index = 0; index < values.Count; index++)
                result[group.Indices[index]] = values[index];
        }
        if (logicalKeys.Count > 0)
            ValidateShardIdentities(_expectedIds);
        return result;
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<CacheKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        return await GetAllAsync<T>(ProjectKeys(keys), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<T>> GetAllAsync<T>(IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var result = new List<T>();
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            result.AddRange(await ReadAsyncValuesBatch<T>(batch, cancellationToken).ConfigureAwait(false));
            batch.Clear();
        }
        if (batch.Count > 0)
            result.AddRange(await ReadAsyncValuesBatch<T>(batch, cancellationToken).ConfigureAwait(false));
        return result;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<CacheKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        return GetAllStreamAsync<T>(ProjectKeys(keys), cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<string> keys,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var batch = new List<string>(_configuration.BatchSize);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            batch.Add(key);
            if (batch.Count < _configuration.BatchSize)
                continue;
            var entries = await ReadAsyncBatch<T>(batch, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entries[index];
            }
            batch.Clear();
        }
        if (batch.Count == 0)
            yield break;
        var tail = await ReadAsyncBatch<T>(batch, cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < tail.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tail[index];
        }
    }

    /// <summary>
    /// 异步按分片读取一批缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">待读取的逻辑键，顺序和重复项均予以保留。</param>
    /// <param name="cancellationToken">用于取消读取的令牌。</param>
    /// <returns>与输入顺序对应的缓存条目数组。</returns>
    private async Task<CacheEntry<T>[]> ReadAsyncBatch<T>(IList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        var entries = new CacheEntry<T>[logicalKeys.Count];
        if (logicalKeys.Count > 0)
            await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var groups = GroupKeys(logicalKeys);
        for (var offset = 0; offset < groups.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Count - offset);
            var tasks = new Task<CacheEntry<T>[]>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = groups[offset + dispatched];
                    tasks[dispatched] = ReadShardBatchAsync<T>(group.Shard, group.Keys, cancellationToken);
                }
                var values = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var shardValues = values[groupIndex];
                    if (shardValues.Length != group.Indices.Count)
                        throw new InvalidOperationException("Redis 分片流式读取返回数量与请求不一致。");
                    for (var index = 0; index < shardValues.Length; index++)
                        entries[group.Indices[index]] = shardValues[index];
                }
            }
            catch
            {
                await Observe(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        if (logicalKeys.Count > 0)
            await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        return entries;
    }

    /// <summary>
    /// 异步按分片读取一批缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">待读取的逻辑键，顺序和重复项均予以保留。</param>
    /// <param name="cancellationToken">用于取消读取的令牌。</param>
    /// <returns>与输入顺序对应的缓存值数组。</returns>
    private async Task<T[]> ReadAsyncValuesBatch<T>(IList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        var values = new T[logicalKeys.Count];
        if (logicalKeys.Count > 0)
            await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var groups = GroupKeys(logicalKeys);
        for (var offset = 0; offset < groups.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Count - offset);
            var tasks = new Task<List<T>>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = groups[offset + dispatched];
                    tasks[dispatched] = _managers[group.Shard].GetAllAsync<T>(group.Keys, cancellationToken);
                }
                var shardValues = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var groupValues = shardValues[groupIndex];
                    if (groupValues.Count != group.Indices.Count)
                        throw new InvalidOperationException("Redis 分片批量读取返回数量与请求不一致。");
                    for (var index = 0; index < groupValues.Count; index++)
                        values[group.Indices[index]] = groupValues[index];
                }
            }
            catch
            {
                await Observe(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        if (logicalKeys.Count > 0)
            await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        return values;
    }

    /// <summary>
    /// 异步从指定分片读取一组缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="keys">待读取的逻辑键。</param>
    /// <param name="cancellationToken">用于取消读取的令牌。</param>
    /// <returns>与输入顺序对应的缓存条目数组。</returns>
    private async Task<CacheEntry<T>[]> ReadShardBatchAsync<T>(int shard, IList<string> keys,
        CancellationToken cancellationToken)
    {
        var entries = new CacheEntry<T>[keys.Count];
        var index = 0;
        await foreach (var entry in _managers[shard].GetAllStreamAsync<T>(keys, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (index == entries.Length)
                throw new InvalidOperationException("Redis 分片流式读取返回数量超出请求。");
            entries[index++] = entry;
        }
        if (index != entries.Length)
            throw new InvalidOperationException("Redis 分片流式读取返回数量不足。");
        return entries;
    }

    #endregion

    #region Batch writes and removes

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<CacheKey, T> items, CacheOptions options = null)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        SetAll(ToItems(items), options);
    }

    /// <inheritdoc />
    public void SetAll<T>(IDictionary<string, T> items, CacheOptions options = null)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            return;
        ValidateShardIdentities(_expectedIds);
        var batches = CreateBatches<T>();
        foreach (var item in items)
        {
            var shard = ShardForKey(item.Key);
            batches[shard][item.Key] = item.Value;
            if (batches[shard].Count < _configuration.BatchSize)
                continue;
            _managers[shard].SetAll(batches[shard], options);
            batches[shard].Clear();
        }
        FlushSets(batches, options);
        ValidateShardIdentities(_expectedIds);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<CacheKey, T> items, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        await SetAllAsync(ToItems(items), options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetAllAsync<T>(IDictionary<string, T> items, CacheOptions options = null,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            return;
        await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var batches = CreateBatches<T>();
        var pending = new List<Task>(_configuration.MaxConcurrency);
        try
        {
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var shard = ShardForKey(item.Key);
                batches[shard][item.Key] = item.Value;
                if (batches[shard].Count < _configuration.BatchSize)
                    continue;
                var batch = batches[shard];
                batches[shard] = new Dictionary<string, T>(StringComparer.Ordinal);
                await QueueBatchAsync(pending,
                    _managers[shard].SetAllAsync(batch, options, cancellationToken))
                    .ConfigureAwait(false);
            }
            for (var shard = 0; shard < _shardCount; shard++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (batches[shard].Count == 0)
                    continue;
                var batch = batches[shard];
                batches[shard] = new Dictionary<string, T>(StringComparer.Ordinal);
                await QueueBatchAsync(pending,
                    _managers[shard].SetAllAsync(batch, options, cancellationToken))
                    .ConfigureAwait(false);
            }
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            await ObservePendingAsync(pending).ConfigureAwait(false);
            throw;
        }
        await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<CacheKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        RemoveAll(ProjectKeys(keys));
    }

    /// <inheritdoc />
    public void RemoveAll(IEnumerable<string> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var batches = CreateKeyBatches();
        var any = false;
        foreach (var key in keys)
        {
            if (!any)
            {
                ValidateShardIdentities(_expectedIds);
                any = true;
            }
            var shard = ShardForKey(key);
            batches[shard].Add(key);
            if (batches[shard].Count < _configuration.BatchSize)
                continue;
            _managers[shard].RemoveAll(batches[shard]);
            batches[shard].Clear();
        }
        FlushRemoves(batches);
        if (any)
            ValidateShardIdentities(_expectedIds);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<CacheKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        await RemoveAllAsync(ProjectKeys(keys), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAllAsync(IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var batches = CreateKeyBatches();
        var pending = new List<Task>(_configuration.MaxConcurrency);
        var any = false;
        try
        {
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!any)
                {
                    await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
                    any = true;
                }
                var shard = ShardForKey(key);
                batches[shard].Add(key);
                if (batches[shard].Count < _configuration.BatchSize)
                    continue;
                var batch = batches[shard];
                batches[shard] = new List<string>(_configuration.BatchSize);
                await QueueBatchAsync(pending,
                    _managers[shard].RemoveAllAsync(batch, cancellationToken))
                    .ConfigureAwait(false);
            }
            for (var shard = 0; shard < _shardCount; shard++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (batches[shard].Count == 0)
                    continue;
                var batch = batches[shard];
                batches[shard] = new List<string>(_configuration.BatchSize);
                await QueueBatchAsync(pending,
                    _managers[shard].RemoveAllAsync(batch, cancellationToken))
                    .ConfigureAwait(false);
            }
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            await ObservePendingAsync(pending).ConfigureAwait(false);
            throw;
        }
        if (any)
            await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
    }

    /// <summary>
    /// 将批量操作加入并发队列，并在达到上限时等待完成。
    /// </summary>
    /// <param name="pending">当前尚未完成的操作队列。</param>
    /// <param name="operation">新派发的异步操作。</param>
    private async Task QueueBatchAsync(List<Task> pending, Task operation)
    {
        pending.Add(operation);
        if (pending.Count < _configuration.MaxConcurrency)
            return;

        var completed = await Task.WhenAny(pending).ConfigureAwait(false);
        pending.Remove(completed);
        await completed.ConfigureAwait(false);
    }

    /// <summary>
    /// 等待并观察已派发的批量操作。
    /// </summary>
    /// <param name="pending">需要观察的异步操作。</param>
    private static async Task ObservePendingAsync(IReadOnlyList<Task> pending)
    {
        if (pending == null || pending.Count == 0)
            return;
        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            // 保留首个批次异常，同时观察其余已经派发的操作。
        }
    }

    /// <summary>
    /// 将缓存键序列转换为逻辑键序列。
    /// </summary>
    /// <param name="keys">待转换的缓存键序列。</param>
    /// <returns>按原顺序投影出的逻辑键序列。</returns>
    private static IEnumerable<string> ProjectKeys(IEnumerable<CacheKey> keys)
    {
        foreach (var key in keys)
        {
            key.Validate();
            yield return key.Key;
        }
    }

    /// <summary>
    /// 将缓存键字典转换为逻辑键字典。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">以缓存键为键的输入字典。</param>
    /// <returns>以逻辑键为键的结果字典。</returns>
    private static IDictionary<string, T> ToItems<T>(IDictionary<CacheKey, T> items)
    {
        var result = new Dictionary<string, T>();
        foreach (var item in items)
        {
            item.Key.Validate();
            result.Add(item.Key.Key, item.Value);
        }
        return result;
    }

    /// <summary>
    /// 创建每个分片对应的批量写入字典。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <returns>按分片索引排列的空字典数组。</returns>
    private Dictionary<string, T>[] CreateBatches<T>()
    {
        var result = new Dictionary<string, T>[_shardCount];
        for (var index = 0; index < result.Length; index++)
            result[index] = new Dictionary<string, T>(StringComparer.Ordinal);
        return result;
    }

    /// <summary>
    /// 创建每个分片对应的批量键列表。
    /// </summary>
    /// <returns>按分片索引排列的空键列表数组。</returns>
    private List<string>[] CreateKeyBatches()
    {
        var result = new List<string>[_shardCount];
        for (var index = 0; index < result.Length; index++)
            result[index] = new List<string>(_configuration.BatchSize);
        return result;
    }

    /// <summary>
    /// 将非空写入批次同步提交到各分片。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="batches">按分片索引排列的写入批次。</param>
    /// <param name="options">应用于写入的缓存选项。</param>
    private void FlushSets<T>(IReadOnlyList<Dictionary<string, T>> batches, CacheOptions options)
    {
        for (var shard = 0; shard < _shardCount; shard++)
            if (batches[shard].Count > 0)
                _managers[shard].SetAll(batches[shard], options);
    }

    /// <summary>
    /// 将非空删除批次同步提交到各分片。
    /// </summary>
    /// <param name="batches">按分片索引排列的待删除键批次。</param>
    private void FlushRemoves(IReadOnlyList<List<string>> batches)
    {
        for (var shard = 0; shard < _shardCount; shard++)
            if (batches[shard].Count > 0)
                _managers[shard].RemoveAll(batches[shard]);
    }

    #endregion

    #region Scan and cleanup

    /// <inheritdoc />
    public List<T> GetByPrefix<T>(string prefix)
        => GetByPrefixAsync<T>(prefix, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<List<T>> GetByPrefixAsync<T>(string prefix,
        CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        var result = new List<T>();
        if (string.IsNullOrEmpty(prefix))
            return result;
        await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var before = _expectedIds;
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        var completed = false;
        try
        {
            var shardResults = await ReadPrefixListsAsync<T>(prefix, scanBudget, cancellationToken)
                .ConfigureAwait(false);
            for (var shard = 0; shard < shardResults.Length; shard++)
                result.AddRange(shardResults[shard]);
            completed = true;
        }
        finally
        {
            if (completed && !cancellationToken.IsCancellationRequested)
                await CacheFactoryExecution.CleanupAsync(
                    () => ValidateShardIdentitiesAsync(CancellationToken.None, before),
                    null, _configuration.Coordination.WaitTimeout).ConfigureAwait(false);
        }
        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamAsync<T>(string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix))
            yield break;
        await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var before = _expectedIds;
        var completed = false;
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        try
        {
            await foreach (var entry in ReadPrefixStreamAsync<T>(prefix, scanBudget, cancellationToken)
                               .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entry;
            }
            completed = true;
        }
        finally
        {
            if (completed && !cancellationToken.IsCancellationRequested)
                await CacheFactoryExecution.CleanupAsync(
                    () => ValidateShardIdentitiesAsync(CancellationToken.None, before),
                    null, _configuration.Coordination.WaitTimeout).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 并行读取各分片中符合前缀的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">待匹配的逻辑键前缀。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    /// <returns>按分片索引排列的缓存值列表。</returns>
    private async Task<List<T>[]> ReadPrefixListsAsync<T>(string prefix, CacheScanBudget scanBudget,
        CancellationToken cancellationToken)
    {
        var results = new List<T>[_shardCount];
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        for (var offset = 0; offset < _shardCount; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, _shardCount - offset);
            var tasks = new Task<List<T>>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var shard = offset + dispatched;
                    tasks[dispatched] = ReadPrefixListShardAsync<T>(shard, prefix, scanBudget, linked.Token);
                }

                var window = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var index = 0; index < window.Length; index++)
                    results[offset + index] = window[index];
            }
            catch
            {
                linked.Cancel();
                await Observe(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        return results;
    }

    /// <summary>
    /// 读取指定分片中符合前缀的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="prefix">待匹配的逻辑键前缀。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    /// <returns>指定分片中符合前缀的缓存值列表。</returns>
    private async Task<List<T>> ReadPrefixListShardAsync<T>(int shard, string prefix,
        CacheScanBudget scanBudget, CancellationToken cancellationToken)
    {
        using var scope = scanBudget.EnterShard(shard);
        try
        {
            var result = await _managers[shard].GetByPrefixWithBudgetAsync<T>(prefix,
                cancellationToken, scanBudget).ConfigureAwait(false);
            scanBudget.MarkShardCompleted(shard);
            return result;
        }
        catch
        {
            scanBudget.MarkShardFailed(shard);
            throw;
        }
    }

    /// <summary>
    /// 以异步流读取各分片中符合前缀的缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="prefix">待匹配的逻辑键前缀。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    /// <returns>按分片扫描结果交错产出的缓存条目流。</returns>
    private async IAsyncEnumerable<CacheEntry<T>> ReadPrefixStreamAsync<T>(string prefix,
        CacheScanBudget scanBudget,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var enumerators = new IAsyncEnumerator<CacheEntry<T>>[_shardCount];
        var pending = new List<PendingPrefixShard<T>>();
        var nextShard = 0;
        try
        {
            while (nextShard < _shardCount && pending.Count < _configuration.MaxConcurrency)
            {
                SchedulePrefixShard(nextShard++, prefix, scanBudget, linked.Token, enumerators, pending);
            }

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var completed = await Task.WhenAny(pending.Select(item => item.Task)).ConfigureAwait(false);
                var pendingItem = pending.First(item => ReferenceEquals(item.Task, completed));
                var shard = pendingItem.Shard;
                pending.Remove(pendingItem);
                if (await completed.ConfigureAwait(false))
                {
                    yield return enumerators[shard].Current;
                    pending.Add(new PendingPrefixShard<T>(shard,
                        MoveNextPrefixShardAsync(shard, enumerators[shard], scanBudget, linked.Token)));
                    continue;
                }

                scanBudget.MarkShardCompleted(shard);
                await enumerators[shard].DisposeAsync();
                enumerators[shard] = null;
                if (nextShard < _shardCount)
                    SchedulePrefixShard(nextShard++, prefix, scanBudget, linked.Token, enumerators, pending);
            }
        }
        finally
        {
            linked.Cancel();
            var remaining = pending.Select(item => item.Task).ToArray();
            await Observe(remaining, remaining.Length).ConfigureAwait(false);
            for (var shard = 0; shard < enumerators.Length; shard++)
                if (enumerators[shard] != null)
                    await enumerators[shard].DisposeAsync();
        }
    }

    /// <summary>
    /// 启动指定分片的前缀扫描并登记其首个移动任务。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="prefix">待匹配的逻辑键前缀。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    /// <param name="enumerators">按分片索引保存的异步枚举器。</param>
    /// <param name="pending">尚未完成的分片移动任务集合。</param>
    private void SchedulePrefixShard<T>(int shard, string prefix, CacheScanBudget scanBudget,
        CancellationToken cancellationToken, IAsyncEnumerator<CacheEntry<T>>[] enumerators,
        ICollection<PendingPrefixShard<T>> pending)
    {
        enumerators[shard] = _managers[shard].GetByPrefixStreamWithBudgetAsync<T>(prefix,
            cancellationToken, scanBudget).GetAsyncEnumerator(cancellationToken);
        pending.Add(new PendingPrefixShard<T>(shard,
            MoveNextPrefixShardAsync(shard, enumerators[shard], scanBudget, cancellationToken)));
    }

    /// <summary>
    /// 推进指定分片的前缀扫描并计入扫描预算。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="enumerator">目标分片的异步枚举器。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    /// <returns>存在下一条缓存条目时为 <see langword="true"/>，扫描结束时为 <see langword="false"/>。</returns>
    private async Task<bool> MoveNextPrefixShardAsync<T>(int shard,
        IAsyncEnumerator<CacheEntry<T>> enumerator, CacheScanBudget scanBudget,
        CancellationToken cancellationToken)
    {
        using var scope = scanBudget.EnterShard(shard);
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        catch
        {
            scanBudget.MarkShardFailed(shard);
            throw;
        }
    }

    /// <summary>
    /// 保存分片前缀扫描的待完成移动任务。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    private sealed class PendingPrefixShard<T>
    {
        /// <summary>
        /// 初始化一个 <see cref="PendingPrefixShard{T}"/> 类型的实例。
        /// </summary>
        /// <param name="shard">目标分片索引。</param>
        /// <param name="task">推进分片扫描的异步任务。</param>
        internal PendingPrefixShard(int shard, Task<bool> task)
        {
            Shard = shard;
            Task = task;
        }

        /// <summary>
        /// 获取任务所属的分片索引。
        /// </summary>
        internal int Shard { get; }

        /// <summary>
        /// 获取推进分片扫描的异步任务。
        /// </summary>
        internal Task<bool> Task { get; }
    }

    /// <inheritdoc />
    public void RemoveByPrefix(string prefix)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix))
            return;
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        RunScan(() => RunSyncShardScans(
            shard => _managers[shard].RemoveByPrefixWithBudget(prefix, scanBudget), scanBudget));
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix,
        CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix))
            return;
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        await RunScanAsync(() => RunShardScansAsync(
            (shard, token) => _managers[shard].RemoveByPrefixWithBudgetAsync(prefix, token, scanBudget),
            scanBudget, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void RemoveByPattern(string pattern)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (string.IsNullOrEmpty(pattern))
            return;
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            Remove(matcher.LiteralPrefix);
            return;
        }
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        RunScan(() => RunSyncShardScans(
            shard => _managers[shard].RemoveByPatternWithBudget(pattern, scanBudget), scanBudget));
    }

    /// <inheritdoc />
    public async Task RemoveByPatternAsync(string pattern,
        CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePattern(_configuration, pattern);
        if (string.IsNullOrEmpty(pattern))
            return;
        var matcher = CachePattern.CreateMatcher(pattern);
        if (matcher.IsLiteral)
        {
            await RemoveAsync(matcher.LiteralPrefix, cancellationToken).ConfigureAwait(false);
            return;
        }
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        await RunScanAsync(() => RunShardScansAsync(
            (shard, token) => _managers[shard].RemoveByPatternWithBudgetAsync(pattern, token, scanBudget),
            scanBudget, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Clear()
    {
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        RunScan(() => RunSyncShardScans(
            shard => _managers[shard].ClearWithBudget(scanBudget), scanBudget));
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var scanBudget = new CacheScanBudget(_configuration.Resources);
        return RunScanAsync(() => RunShardScansAsync(
            (shard, token) => _managers[shard].ClearWithBudgetAsync(token, scanBudget),
            scanBudget, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// 以有界并发执行所有分片的同步扫描委托。
    /// </summary>
    /// <param name="action">接收分片索引的扫描委托。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    private void RunSyncShardScans(Action<int> action, CacheScanBudget scanBudget)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        if (_configuration.SyncCleanupMode == CacheSyncCleanupMode.Sequential)
        {
            for (var shard = 0; shard < _shardCount; shard++)
            {
                scanBudget.SetShardProgress(shard, shard);
                action(shard);
            }
            return;
        }

        for (var offset = 0; offset < _shardCount; offset += _configuration.MaxConcurrency)
        {
            var count = Math.Min(_configuration.MaxConcurrency, _shardCount - offset);
            var tasks = new Task[count];
            for (var index = 0; index < count; index++)
            {
                var shard = offset + index;
                tasks[index] = Task.Factory.StartNew(
                    () => RunSyncShardScan(shard, action, scanBudget),
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            WaitForSyncShardWindow(tasks, offset);
        }
    }

    /// <summary>
    /// 执行单个分片的同步扫描并释放预算范围。
    /// </summary>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="action">接收分片索引的扫描委托。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    private static void RunSyncShardScan(int shard, Action<int> action, CacheScanBudget scanBudget)
    {
        using var scope = scanBudget.EnterShard(shard);
        try
        {
            action(shard);
            scanBudget.MarkShardCompleted(shard);
        }
        catch
        {
            scanBudget.MarkShardFailed(shard);
            throw;
        }
    }

    /// <summary>
    /// 等待并观察从指定位置开始的同步分片任务。
    /// </summary>
    /// <param name="tasks">已经派发的分片任务。</param>
    /// <param name="windowStart">本次等待窗口的起始索引。</param>
    private static void WaitForSyncShardWindow(IReadOnlyList<Task> tasks, int windowStart)
    {
        try
        {
            Task.WhenAll(tasks).GetAwaiter().GetResult();
        }
        catch
        {
            for (var index = 0; index < tasks.Count; index++)
            {
                var task = tasks[index];
                if (task.IsFaulted && task.Exception?.InnerExceptions.Count > 0)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
                        task.Exception.InnerExceptions[0]).Throw();
                if (task.IsCanceled)
                    throw new OperationCanceledException(
                        $"缓存分片同步清理窗口 {windowStart} 中的分片 {windowStart + index} 被取消。");
            }
            throw;
        }
    }

    /// <summary>
    /// 以有界并发异步执行所有分片的扫描委托。
    /// </summary>
    /// <param name="action">接收分片索引和取消令牌的扫描委托。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    private async Task RunShardScansAsync(Func<int, CancellationToken, Task> action,
        CacheScanBudget scanBudget, CancellationToken cancellationToken)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (scanBudget == null)
            throw new ArgumentNullException(nameof(scanBudget));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        for (var offset = 0; offset < _shardCount; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, _shardCount - offset);
            var tasks = new Task[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var shard = offset + dispatched;
                    tasks[dispatched] = RunShardScanAsync(shard, action, scanBudget, linked.Token);
                }
                await ObserveShardWindowAsync(tasks, dispatched, linked).ConfigureAwait(false);
            }
            catch
            {
                linked.Cancel();
                await Observe(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// 执行单个分片的异步扫描并释放预算范围。
    /// </summary>
    /// <param name="shard">目标分片索引。</param>
    /// <param name="action">接收分片索引和取消令牌的扫描委托。</param>
    /// <param name="scanBudget">用于限制扫描资源的预算。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    private async Task RunShardScanAsync(int shard, Func<int, CancellationToken, Task> action,
        CacheScanBudget scanBudget, CancellationToken cancellationToken)
    {
        using var scope = scanBudget.EnterShard(shard);
        try
        {
            await action(shard, cancellationToken).ConfigureAwait(false);
            scanBudget.MarkShardCompleted(shard);
        }
        catch
        {
            scanBudget.MarkShardFailed(shard);
            throw;
        }
    }

    /// <summary>
    /// 等待并观察一组分片任务，并在失败时取消同一窗口中的其他任务。
    /// </summary>
    /// <param name="tasks">已经派发的分片任务。</param>
    /// <param name="count">需要观察的任务数量。</param>
    /// <param name="linked">用于协调取消的令牌源。</param>
    private static async Task ObserveShardWindowAsync(IReadOnlyList<Task> tasks, int count,
        CancellationTokenSource linked)
    {
        var pending = new List<Task>(count);
        for (var index = 0; index < count; index++)
            pending.Add(tasks[index]);

        while (pending.Count > 0)
        {
            var completed = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(completed);
            try
            {
                await completed.ConfigureAwait(false);
            }
            catch
            {
                linked.Cancel();
                await Observe(pending, pending.Count).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// 在扫描前后校验分片身份并执行同步扫描。
    /// </summary>
    /// <param name="action">要执行的同步扫描操作。</param>
    private void RunScan(Action action)
    {
        ValidateShardIdentities(_expectedIds);
        var before = _expectedIds;
        var completed = false;
        try
        {
            action();
            completed = true;
        }
        finally
        {
            if (completed)
                CacheFactoryExecution.Cleanup(() => ValidateShardIdentities(before), null);
        }
    }

    /// <summary>
    /// 在扫描前后异步校验分片身份并执行扫描。
    /// </summary>
    /// <param name="action">要执行的异步扫描操作。</param>
    /// <param name="cancellationToken">用于取消扫描的令牌。</param>
    private async Task RunScanAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await ValidateShardIdentitiesAsync(cancellationToken, _expectedIds).ConfigureAwait(false);
        var before = _expectedIds;
        var completed = false;
        Exception primary = null;
        try
        {
            await action().ConfigureAwait(false);
            completed = true;
        }
        catch (Exception error)
        {
            primary = error;
            throw;
        }
        finally
        {
            if (completed && !cancellationToken.IsCancellationRequested)
                await CacheFactoryExecution.CleanupAsync(
                    () => ValidateShardIdentitiesAsync(CancellationToken.None, before),
                    primary, _configuration.Coordination.WaitTimeout).ConfigureAwait(false);
        }
    }

    #endregion

    #region Routing and topology validation

    /// <summary>
    /// 将键操作路由到所属分片并校验分片身份。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="key">待路由的逻辑键。</param>
    /// <param name="action">要在所属分片执行的操作。</param>
    /// <returns>分片操作返回的结果。</returns>
    private TResult ExecuteForKey<TResult>(string key, Func<CacheManager, TResult> action)
    {
        var shard = ShardForKey(key);
        ValidateShardIdentity(shard);
        var result = action(_managers[shard]);
        ValidateShardIdentity(shard);
        return result;
    }

    /// <summary>
    /// 将键操作路由到所属分片并校验分片身份。
    /// </summary>
    /// <param name="key">待路由的逻辑键。</param>
    /// <param name="action">要在所属分片执行的操作。</param>
    private void ExecuteForKey(string key, Action<CacheManager> action)
    {
        var shard = ShardForKey(key);
        ValidateShardIdentity(shard);
        action(_managers[shard]);
        ValidateShardIdentity(shard);
    }

    /// <summary>
    /// 异步将键操作路由到所属分片并校验分片身份。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="key">待路由的逻辑键。</param>
    /// <param name="action">要在所属分片执行的异步操作。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>异步操作完成后的结果。</returns>
    private async Task<TResult> ExecuteForKeyAsync<TResult>(string key,
        Func<CacheManager, Task<TResult>> action, CancellationToken cancellationToken)
    {
        var shard = ShardForKey(key);
        await ValidateShardIdentityAsync(shard, cancellationToken).ConfigureAwait(false);
        var result = await action(_managers[shard]).ConfigureAwait(false);
        await ValidateShardIdentityAsync(shard, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 异步将键操作路由到所属分片并校验分片身份。
    /// </summary>
    /// <param name="key">待路由的逻辑键。</param>
    /// <param name="action">要在所属分片执行的异步操作。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    private async Task ExecuteForKeyAsync(string key, Func<CacheManager, Task> action,
        CancellationToken cancellationToken)
    {
        var shard = ShardForKey(key);
        await ValidateShardIdentityAsync(shard, cancellationToken).ConfigureAwait(false);
        await action(_managers[shard]).ConfigureAwait(false);
        await ValidateShardIdentityAsync(shard, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 根据物理键计算所属客户端分片。
    /// </summary>
    /// <param name="key">待路由的逻辑键。</param>
    /// <returns>所属分片的零基索引。</returns>
    private int ShardForKey(string key)
    {
        var physicalKey = CacheKeyFormat.GetPhysicalKey(_configuration, key);
        var start = physicalKey.IndexOf('{');
        var end = start < 0 ? -1 : physicalKey.IndexOf('}', start + 1);
        if (start < 0 || end - start - 1 < 8)
            throw new InvalidOperationException("缓存物理键缺少客户端分片路由标签。");
        var tag = physicalKey.Substring(start + 1, 8);
        if (!uint.TryParse(tag, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException("缓存物理键的客户端分片路由标签无效。");
        return (int)(value % (uint)_shardCount);
    }

    /// <summary>
    /// 按所属分片对逻辑键分组并保留原始位置。
    /// </summary>
    /// <param name="keys">待分组的逻辑键列表。</param>
    /// <returns>按分片索引排列且记录输入位置的分组。</returns>
    private List<ShardKeyGroup> GroupKeys(IList<string> keys)
    {
        var groups = new ShardKeyGroup[_shardCount];
        for (var index = 0; index < keys.Count; index++)
        {
            var shard = ShardForKey(keys[index]);
            var group = groups[shard];
            if (group == null)
                groups[shard] = group = new ShardKeyGroup(shard);
            group.Keys.Add(keys[index]);
            group.Indices.Add(index);
        }
        var result = new List<ShardKeyGroup>();
        for (var shard = 0; shard < groups.Length; shard++)
            if (groups[shard] != null)
                result.Add(groups[shard]);
        return result;
    }

    /// <summary>
    /// 保存同一分片的逻辑键及其输入位置。
    /// </summary>
    private sealed class ShardKeyGroup
    {
        /// <summary>
        /// 初始化一个 <see cref="ShardKeyGroup"/> 类型的实例。
        /// </summary>
        /// <param name="shard">分组所属的分片索引。</param>
        internal ShardKeyGroup(int shard) { Shard = shard; }

        /// <summary>
        /// 获取分组所属的分片索引。
        /// </summary>
        internal int Shard { get; }

        /// <summary>
        /// 获取按输入顺序排列的逻辑键。
        /// </summary>
        internal List<string> Keys { get; } = new();

        /// <summary>
        /// 获取各逻辑键在原始输入中的位置。
        /// </summary>
        internal List<int> Indices { get; } = new();
    }

    /// <summary>
    /// 查询并校验当前各分片的身份标识。
    /// </summary>
    /// <returns>按分片顺序排列的身份标识数组。</returns>
    private string[] ValidateShardIdentities()
    {
        var infos = new string[_shardCount];
        for (var index = 0; index < _shardCount; index++)
        {
            ValidateSingleMaster(_databases[index]);
            infos[index] = _databases[index].Execute("INFO", "SERVER").ToString();
        }
        CacheShardTopology.Validate(infos, _shardCount);
        var ids = infos.Select(CacheShardTopology.GetServerId).ToArray();
        return ids;
    }

    /// <summary>
    /// 校验指定分片仍连接到预期的单主节点。
    /// </summary>
    /// <param name="shard">待校验的分片索引。</param>
    private void ValidateShardIdentity(int shard)
    {
        var database = _databases[shard];
        ValidateSingleMaster(database);
        var actual = CacheShardTopology.GetServerId(database.Execute("INFO", "SERVER").ToString());
        if (!string.Equals(actual, _expectedIds[shard], StringComparison.Ordinal))
            throw new InvalidOperationException("Redis 客户端分片身份已变化。");
    }

    /// <summary>
    /// 异步校验指定分片仍连接到预期的单主节点。
    /// </summary>
    /// <param name="shard">待校验的分片索引。</param>
    /// <param name="cancellationToken">用于取消校验的令牌。</param>
    private async Task ValidateShardIdentityAsync(int shard, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = _databases[shard];
        ValidateSingleMaster(database);
        var info = await database.ExecuteAsync("INFO", "SERVER").ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var actual = CacheShardTopology.GetServerId(info.ToString());
        if (!string.Equals(actual, _expectedIds[shard], StringComparison.Ordinal))
            throw new InvalidOperationException("Redis 客户端分片身份已变化。");
    }

    /// <summary>
    /// 校验当前所有分片身份与预期值一致。
    /// </summary>
    /// <param name="expected">预期的分片身份标识。</param>
    private void ValidateShardIdentities(IReadOnlyList<string> expected)
    {
        var actual = ValidateShardIdentities();
        if (actual.Length != expected.Count ||
            actual.Where((id, index) => !string.Equals(id, expected[index], StringComparison.Ordinal)).Any())
            throw new InvalidOperationException("Redis 客户端分片身份在操作期间发生变化。");
    }

    /// <summary>
    /// 异步查询分片身份，并在提供预期值时执行一致性校验。
    /// </summary>
    /// <param name="cancellationToken">用于取消校验的令牌。</param>
    /// <param name="expected">可选的预期身份标识；为 <see langword="null"/> 时仅查询身份。</param>
    /// <returns>按分片顺序排列的当前身份标识。</returns>
    private async Task<string[]> ValidateShardIdentitiesAsync(CancellationToken cancellationToken,
        IReadOnlyList<string> expected = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var infos = new string[_shardCount];
        for (var index = 0; index < _shardCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSingleMaster(_databases[index]);
            var info = await _databases[index].ExecuteAsync("INFO", "SERVER").ConfigureAwait(false);
            infos[index] = info.ToString();
        }
        CacheShardTopology.Validate(infos, _shardCount);
        var ids = infos.Select(CacheShardTopology.GetServerId).ToArray();
        if (expected != null && (ids.Length != expected.Count ||
            ids.Where((id, index) => !string.Equals(id, expected[index], StringComparison.Ordinal)).Any()))
            throw new InvalidOperationException("Redis 客户端分片身份在操作期间发生变化。");
        return ids;
    }

    /// <summary>
    /// 确认数据库连接仅对应一个可用主节点。
    /// </summary>
    /// <param name="database">待检查的 Redis 数据库。</param>
    private static void ValidateSingleMaster(IDatabase database)
    {
        var endpoints = database.Multiplexer.GetEndPoints();
        if (endpoints.Length != 1)
            throw new InvalidOperationException("客户端分片的每个 Provider 必须连接一个独立 Redis 主节点。");
        var server = database.Multiplexer.GetServer(endpoints[0]);
        if (!server.IsConnected || server.IsReplica)
            throw new InvalidOperationException("Redis 分片主节点不可用或已变为副本。");
    }

    /// <summary>
    /// 等待并观察指定数量的异步任务。
    /// </summary>
    /// <typeparam name="T">任务结果类型。</typeparam>
    /// <param name="tasks">需要观察的异步任务。</param>
    /// <param name="count">需要观察的任务数量。</param>
    private static async Task Observe<T>(IReadOnlyList<Task<T>> tasks, int count)
    {
        for (var index = 0; index < count; index++)
        {
            try { await tasks[index].ConfigureAwait(false); }
            catch { }
        }
    }

    /// <summary>
    /// 等待并观察指定数量的异步任务。
    /// </summary>
    /// <param name="tasks">需要观察的异步任务。</param>
    /// <param name="count">需要观察的任务数量。</param>
    private static async Task Observe(IReadOnlyList<Task> tasks, int count)
    {
        for (var index = 0; index < count; index++)
        {
            try { await tasks[index].ConfigureAwait(false); }
            catch { }
        }
    }

    #endregion
}
