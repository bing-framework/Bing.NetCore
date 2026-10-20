using System.Runtime.CompilerServices;
using System.Text;
using CSRedis;
using Newtonsoft.Json;

namespace Bing.Caching.CSRedis;

/// <inheritdoc />
public sealed partial class CSRedisCacheManager
{
    /// <summary>
    /// 同步批量读取原始缓存值。
    /// </summary>
    /// <param name="keys">待读取的物理键。</param>
    /// <returns>对应顺序的原始缓存值。</returns>
    private byte[][] ReadRawBatch(string[] keys) =>
        CacheBatchAdaptive.Read(keys,
            batch => _client.MGet<byte[]>(batch),
            (batch, values) => ValidateRawBatch(batch, values));

    /// <summary>
    /// 异步批量读取原始缓存值。
    /// </summary>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>对应顺序的原始缓存值。</returns>
    private async Task<byte[][]> ReadRawBatchAsync(string[] keys, CancellationToken cancellationToken) =>
        await CacheBatchAdaptive.ReadAsync(keys,
            batch => CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead,
                () => _client.MGetAsync<byte[]>(batch)),
            (batch, values) => ValidateRawBatch(batch, values),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 同步批量读取原始缓存值。
    /// </summary>
    /// <param name="client">执行读取的客户端。</param>
    /// <param name="keys">待读取的物理键。</param>
    /// <returns>对应顺序的原始缓存值。</returns>
    private byte[][] ReadRawBatch(CSRedisClient client, string[] keys) =>
        CacheBatchAdaptive.Read(keys,
            batch => client.MGet<byte[]>(batch),
            (batch, values) => ValidateRawBatch(batch, values));

    /// <summary>
    /// 异步批量读取原始缓存值。
    /// </summary>
    /// <param name="client">执行读取的客户端。</param>
    /// <param name="keys">待读取的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>对应顺序的原始缓存值。</returns>
    private async Task<byte[][]> ReadRawBatchAsync(CSRedisClient client, string[] keys,
        CancellationToken cancellationToken) =>
        await CacheBatchAdaptive.ReadAsync(keys,
            batch => CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead,
                () => client.MGetAsync<byte[]>(batch)),
            (batch, values) => ValidateRawBatch(batch, values),
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 校验批量读取结果与请求数量及资源限制。
    /// </summary>
    /// <param name="keys">原始请求键。</param>
    /// <param name="values">Redis 返回值。</param>
    private void ValidateRawBatch(string[] keys, byte[][] values)
    {
        if (values == null || values.Length != keys.Length)
            throw new InvalidOperationException("Redis MGET 返回数量与请求不一致。");
        CacheBatchBudget.Check(values, _configuration.Resources.MaxBatchPayloadBytes);
    }

    /// <summary>
    /// 解码已校验的原始批次。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="raw">原始缓存值。</param>
    /// <returns>解码后的缓存值序列。</returns>
    private IEnumerable<T> ReadValidatedBatch<T>(byte[][] raw)
    {
        foreach (var value in raw) yield return Read<T>(value);
    }

    /// <summary>
    /// 校验并解码单键读取结果。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="raw">原始缓存值。</param>
    /// <returns>解码后的缓存值。</returns>
    private T ReadBudgetValue<T>(byte[] raw)
    {
        CacheBatchBudget.Check(new[] { raw }, _configuration.Resources.MaxBatchPayloadBytes);
        return Read<T>(raw);
    }
    /// <inheritdoc />
    public IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<CacheKey> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        return GetAllStreamAsync<T>(ProjectKeys(keys), cancellationToken);
    }

    /// <summary>
    /// 将缓存键序列转换为逻辑键序列。
    /// </summary>
    /// <param name="keys">缓存键序列。</param>
    /// <returns>逻辑键序列。</returns>
    private static IEnumerable<string> ProjectKeys(IEnumerable<CacheKey> keys)
    {
        foreach (var key in keys)
        {
            key.Validate();
            yield return key.Key;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetAllStreamAsync<T>(IEnumerable<string> keys,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        // 输入只枚举一次；完整批次直接复用数组，尾批才复制精确长度。
        var logicalBatch = new string[_configuration.BatchSize];
        var logicalCount = 0;
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logicalBatch[logicalCount++] = key;
            if (logicalCount < logicalBatch.Length)
                continue;
            var entries = await ReadInputBatchAsync<T>(logicalBatch, cancellationToken).ConfigureAwait(false);
            for (var index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entries[index];
            }
            Array.Clear(logicalBatch, 0, logicalCount);
            logicalCount = 0;
        }
        if (logicalCount == 0)
            yield break;
        var tail = await ReadInputBatchAsync<T>(CopyBatch(logicalBatch, logicalCount), cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < tail.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tail[index];
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CacheEntry<T>> GetByPrefixStreamAsync<T>(string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        CacheInputValidator.ValidatePrefix(_configuration, prefix);
        if (string.IsNullOrEmpty(prefix))
            yield break;

        var logicalKeys = new string[_configuration.BatchSize];
        var physicalKeys = new string[_configuration.BatchSize];
        var count = 0;
        await foreach (var item in EnumerateMatchingKeysAsync(prefix, cancellationToken))
        {
            logicalKeys[count] = item.LogicalKey;
            physicalKeys[count++] = item.PhysicalKey;
            if (count < logicalKeys.Length)
                continue;

            var entries = await ReadPrefixBatchAsync<T>(physicalKeys, logicalKeys, cancellationToken)
                .ConfigureAwait(false);
            for (var index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entries[index];
            }
            Array.Clear(logicalKeys, 0, count);
            Array.Clear(physicalKeys, 0, count);
            count = 0;
        }

        if (count == 0)
            yield break;
        var tail = await ReadPrefixBatchAsync<T>(CopyBatch(physicalKeys, count),
            CopyBatch(logicalKeys, count), cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < tail.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tail[index];
        }
    }

    /// <summary>
    /// 按当前拓扑读取一批输入键并构造命中结果。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">逻辑键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>保留输入顺序的缓存条目。</returns>
    private async Task<CacheEntry<T>[]> ReadInputBatchAsync<T>(IList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        var count = logicalKeys.Count;
        var physicalKeys = CacheKeyFormat.GetPhysicalKeys(_configuration, logicalKeys);
        var entries = new CacheEntry<T>[count];
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            await FillClusterEntriesAsync(physicalKeys, logicalKeys, entries, cancellationToken)
                .ConfigureAwait(false);
            return entries;
        }

        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            await FillShardedEntriesAsync(physicalKeys, logicalKeys, entries, cancellationToken)
                .ConfigureAwait(false);
            return entries;
        }

        var rawBatch = await ReadRawBatchAsync(physicalKeys, cancellationToken).ConfigureAwait(false);
        FillEntries(logicalKeys, entries, rawBatch);
        return entries;
    }

    /// <summary>
    /// 按当前拓扑读取一批值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入物理键顺序一致的值。</returns>
    /// <remarks>此路径不创建 <see cref="CacheEntry{T}"/>，供列表 API 使用。</remarks>
    private async Task<T[]> ReadValuesBatchAsync<T>(IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        if (physicalKeys.Count == 0)
            return Array.Empty<T>();

        if (_configuration.Topology == CacheTopology.RedisCluster)
            return await ReadClusterValuesAsync<T>(physicalKeys, cancellationToken).ConfigureAwait(false);

        if (_configuration.Topology == CacheTopology.ClientSharded)
            return await ReadShardedValuesAsync<T>(physicalKeys, cancellationToken).ConfigureAwait(false);

        var rawBatch = await ReadRawBatchAsync(GetBatchArray(physicalKeys), cancellationToken).ConfigureAwait(false);
        return DeserializeValues<T>(rawBatch);
    }

    /// <summary>
    /// 按 Redis Cluster 槽位分组读取一批缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入物理键顺序一致的值。</returns>
    private async Task<T[]> ReadClusterValuesAsync<T>(IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        var groups = ClusterGroups(physicalKeys).ToArray();
        var values = new T[physicalKeys.Count];
        for (var offset = 0; offset < groups.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Length - offset);
            var tasks = new Task<byte[][]>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = groups[offset + dispatched];
                    var batch = new string[group.Value.Count];
                    for (var index = 0; index < batch.Length; index++)
                        batch[index] = physicalKeys[group.Value[index]];
                    tasks[dispatched] = ReadRawBatchAsync(_client, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var decoded = DeserializeValues<T>(rawGroups[groupIndex]);
                    for (var index = 0; index < decoded.Length; index++)
                        values[group.Value[index]] = decoded[index];
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        return values;
    }

    /// <summary>
    /// 按客户端分片路由批量读取缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入物理键顺序一致的值。</returns>
    private async Task<T[]> ReadShardedValuesAsync<T>(IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        var values = new T[physicalKeys.Count];
        var groups = new Dictionary<int, List<int>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var nodeIndex = _shardNodeIndex(physicalKeys[index]);
            if (nodeIndex < 0 || nodeIndex >= _scanClients.Count)
                throw new InvalidOperationException("Redis 分片路由返回无效节点索引。");
            if (!groups.TryGetValue(nodeIndex, out var indexes))
                groups.Add(nodeIndex, indexes = new List<int>());
            indexes.Add(index);
        }

        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task<byte[][]>[windowCount];
            var dispatched = 0;
            try
            {
                for (; dispatched < windowCount; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = grouped[offset + dispatched];
                    var batch = new string[group.Value.Count];
                    for (var index = 0; index < batch.Length; index++)
                        batch[index] = physicalKeys[group.Value[index]];
                    tasks[dispatched] = ReadRawBatchAsync(_scanClients[group.Key], batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < windowCount; groupIndex++)
                {
                    var group = grouped[offset + groupIndex];
                    var decoded = DeserializeValues<T>(rawGroups[groupIndex]);
                    for (var index = 0; index < decoded.Length; index++)
                        values[group.Value[index]] = decoded[index];
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        return values;
    }

    /// <summary>
    /// 读取前缀扫描得到的一批键并构造缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="logicalKeys">对应的逻辑键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>扫描批次对应的缓存条目。</returns>
    private async Task<CacheEntry<T>[]> ReadPrefixBatchAsync<T>(IList<string> physicalKeys,
        IList<string> logicalKeys,
        CancellationToken cancellationToken)
    {
        var entries = new CacheEntry<T>[logicalKeys.Count];

        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            await FillClusterEntriesAsync(physicalKeys, logicalKeys, entries, cancellationToken)
                .ConfigureAwait(false);
            return entries;
        }

        if (_configuration.Topology == CacheTopology.ClientSharded)
        {
            await FillShardedEntriesAsync(physicalKeys, logicalKeys, entries, cancellationToken)
                .ConfigureAwait(false);
            return entries;
        }

        var rawBatch = await ReadRawBatchAsync(GetBatchArray(physicalKeys), cancellationToken).ConfigureAwait(false);
        FillEntries(logicalKeys, entries, rawBatch);
        return entries;
    }

    /// <summary>
    /// 按 Cluster 槽位读取并填充缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="logicalKeys">对应的逻辑键批次。</param>
    /// <param name="entries">接收结果的数组。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task FillClusterEntriesAsync<T>(IList<string> physicalKeys, IList<string> logicalKeys,
        CacheEntry<T>[] entries, CancellationToken cancellationToken)
    {
        var groups = ClusterGroups(physicalKeys).ToArray();
        for (var offset = 0; offset < groups.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Length - offset);
            var tasks = new Task<byte[][]>[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = groups[offset + dispatched];
                    var groupedKeys = new string[group.Value.Count];
                    for (var index = 0; index < groupedKeys.Length; index++)
                        groupedKeys[index] = physicalKeys[group.Value[index]];
                    tasks[dispatched] = ReadRawBatchAsync(_client, groupedKeys, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var batch = rawGroups[groupIndex];
                    FillEntries(logicalKeys, entries, batch, group.Value);
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// 按客户端分片读取并填充缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="logicalKeys">对应的逻辑键批次。</param>
    /// <param name="entries">接收结果的数组。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task FillShardedEntriesAsync<T>(IList<string> physicalKeys, IList<string> logicalKeys,
        CacheEntry<T>[] entries, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<int, List<int>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var nodeIndex = _shardNodeIndex(physicalKeys[index]);
            if (nodeIndex < 0 || nodeIndex >= _scanClients.Count)
                throw new InvalidOperationException("Redis 分片路由返回无效节点索引。");
            if (!groups.TryGetValue(nodeIndex, out var indexes))
                groups.Add(nodeIndex, indexes = new List<int>());
            indexes.Add(index);
        }
        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task<byte[][]>[windowCount];
            var dispatched = 0;
            try
            {
                for (; dispatched < windowCount; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var group = grouped[offset + dispatched];
                    var batch = new string[group.Value.Count];
                    for (var index = 0; index < batch.Length; index++)
                        batch[index] = physicalKeys[group.Value[index]];
                    tasks[dispatched] = ReadRawBatchAsync(_scanClients[group.Key], batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < windowCount; groupIndex++)
                {
                    var group = grouped[offset + groupIndex];
                    FillEntries(logicalKeys, entries, rawGroups[groupIndex], group.Value);
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// 观察已派发的任务并忽略其后续异常。
    /// </summary>
    /// <typeparam name="T">任务结果类型。</typeparam>
    /// <param name="tasks">已派发任务数组。</param>
    /// <param name="count">需要观察的任务数。</param>
    private static async Task ObservePendingAsync<T>(Task<T>[] tasks, int count)
    {
        for (var index = 0; index < count; index++)
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
    }

    /// <summary>
    /// 复用批量读取已经拥有的数组；扫描路径传入列表时仍创建客户端所需的连续数组。
    /// </summary>
    /// <param name="physicalKeys">物理键列表。</param>
    /// <returns>可供批量读取使用的物理键数组。</returns>
    private static string[] GetBatchArray(IList<string> physicalKeys)
    {
        if (physicalKeys is string[] array)
            return array;

        var result = new string[physicalKeys.Count];
        for (var index = 0; index < result.Length; index++)
            result[index] = physicalKeys[index];
        return result;
    }

    /// <summary>
    /// 复制指定长度的批次数组。
    /// </summary>
    /// <param name="values">源数组。</param>
    /// <param name="count">要复制的元素数。</param>
    /// <returns>指定长度的批次数组。</returns>
    private static string[] CopyBatch(string[] values, int count)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (count < 0 || count > values.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (count == values.Length)
            return values;
        var result = new string[count];
        Array.Copy(values, result, count);
        return result;
    }

    /// <summary>
    /// 将原始批量响应反序列化为指定类型。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="rawValues">Redis 返回的原始缓存值。</param>
    /// <returns>对应顺序的缓存值。</returns>
    private T[] DeserializeValues<T>(byte[][] rawValues)
    {
        if (rawValues.Length == 0)
            return Array.Empty<T>();

        var valueType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (JsonConvert.DefaultSettings != null ||
            !(valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(decimal)))
        {
            var decoded = new T[rawValues.Length];
            for (var index = 0; index < rawValues.Length; index++)
                decoded[index] = Read<T>(rawValues[index]);
            return decoded;
        }

        // 每个 Redis 响应批次只建立一次 JSON 解析器，原始 null 仍由 Hit 单独表达。
        // 非 nullable 值类型不能直接反序列化 JSON null，先用一次序列化的 default token 占位。
        var defaultValueJson = typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null
            ? JsonConvert.SerializeObject(default(T))
            : null;
        var payload = CacheJsonBatch.Build(rawValues, defaultValueJson);
        // Newtonsoft.Json 会把标量数组中的 null 映射为 default(T)；Hit 仍由原始响应单独判断。
        // 直接使用泛型数组，避免 Nullable<T> 反射数组和 Array.GetValue 的逐项装箱。
        var values = JsonConvert.DeserializeObject<T[]>(payload);
        if (values == null || values.Length != rawValues.Length)
            throw new InvalidOperationException("Redis 批量反序列化结果与请求数量不一致。");
        return values;
    }

    /// <summary>
    /// 将批量响应映射为缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="logicalKeys">逻辑键批次。</param>
    /// <param name="entries">接收结果的数组。</param>
    /// <param name="rawValues">原始 Redis 响应。</param>
    /// <param name="indexes">可选的目标索引映射。</param>
    private void FillEntries<T>(IList<string> logicalKeys, CacheEntry<T>[] entries,
        byte[][] rawValues, IList<int> indexes = null)
    {
        var values = DeserializeValues<T>(rawValues);
        for (var index = 0; index < rawValues.Length; index++)
        {
            var target = indexes == null ? index : indexes[index];
            entries[target] = new CacheEntry<T>(logicalKeys[target], values[index], rawValues[index] != null);
        }
    }

    /// <summary>
    /// 按 Redis 槽位对物理键索引分组。
    /// </summary>
    /// <param name="physicalKeys">物理键列表。</param>
    /// <returns>槽位对应的输入索引集合。</returns>
    private static Dictionary<int, List<int>> ClusterGroups(IList<string> physicalKeys)
    {
        var groups = new Dictionary<int, List<int>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var slot = CacheClusterTopology.GetHashSlot(physicalKeys[index]);
            if (!groups.TryGetValue(slot, out var indexes))
                groups.Add(slot, indexes = new List<int>());
            indexes.Add(index);
        }
        return groups;
    }

    /// <summary>
    /// 扫描并流式枚举指定逻辑前缀下的物理键和逻辑键。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配键的物理键和逻辑键。</returns>
    private async IAsyncEnumerable<(string PhysicalKey, string LogicalKey)> EnumerateMatchingKeysAsync(string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topology = await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanClient in _scanClients)
        foreach (var entry in scanClient.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = entry.Key;
            if (_configuration.Topology == CacheTopology.RedisCluster)
            {
                using var identityConnection = await entry.Value.GetAsync().ConfigureAwait(false);
                identity = ClusterText(await identityConnection.Value.CallAsync("CLUSTER", new[] { "MYID" })
                    .ConfigureAwait(false));
            }
            if (!visitedNodes.Add(identity))
                continue;
            var node = entry.Value;
            using var connection = await node.GetAsync().ConfigureAwait(false);
            if (!string.Equals((await connection.Value.RoleAsync().ConfigureAwait(false)).RoleName,
                    "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
            long cursor = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.ConsumePage();
                global::CSRedis.RedisScan<string> page = await CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan,
                    () => connection.Value.ScanAsync(cursor,
                    scanPattern, _configuration.BatchSize))
                    .ConfigureAwait(false);
                cursor = page.Cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.Items)
                {
                    CacheCommandProbe.RecordScanItem();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physicalKey, encodedPrefix,
                            out var logicalKey))
                    {
                        budget.Consume();
                        yield return (physicalKey, logicalKey);
                    }
                }
            } while (cursor != 0);
            if (!string.Equals((await connection.Value.RoleAsync().ConfigureAwait(false)).RoleName,
                    "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
        }
        CacheClusterTopology.EnsureUnchanged(topology,
            await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 扫描并流式枚举指定逻辑前缀下的物理键。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配键的物理键序列。</returns>
    private async IAsyncEnumerable<string> EnumerateMatchingPhysicalKeysAsync(string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topology = await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        var visitedNodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scanClient in _scanClients)
        foreach (var entry in scanClient.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = entry.Key;
            if (_configuration.Topology == CacheTopology.RedisCluster)
            {
                using var identityConnection = await entry.Value.GetAsync().ConfigureAwait(false);
                identity = ClusterText(await identityConnection.Value.CallAsync("CLUSTER", new[] { "MYID" })
                    .ConfigureAwait(false));
            }
            if (!visitedNodes.Add(identity))
                continue;
            var node = entry.Value;
            using var connection = await node.GetAsync().ConfigureAwait(false);
            if (!string.Equals((await connection.Value.RoleAsync().ConfigureAwait(false)).RoleName,
                    "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
            long cursor = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.ConsumePage();
                global::CSRedis.RedisScan<string> page = await CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan,
                    () => connection.Value.ScanAsync(cursor,
                    scanPattern, _configuration.BatchSize))
                    .ConfigureAwait(false);
                cursor = page.Cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.Items)
                {
                    CacheCommandProbe.RecordScanItem();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physicalKey, prefix, encodedPrefix))
                    {
                        budget.Consume();
                        yield return physicalKey;
                    }
                }
            } while (cursor != 0);
            if (!string.Equals((await connection.Value.RoleAsync().ConfigureAwait(false)).RoleName,
                    "master", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("扫描节点不再是 Redis 主节点。");
        }
        CacheClusterTopology.EnsureUnchanged(topology,
            await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false));
    }
}
