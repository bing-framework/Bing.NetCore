using System.Runtime.CompilerServices;
using System.Text;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis;

/// <inheritdoc />
public partial class FreeRedisCacheManager
{
    /// <summary>
    /// 同步读取一批物理键并反序列化缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="client">执行读取的 Redis 客户端。</param>
    /// <param name="keys">物理键批次。</param>
    /// <returns>与输入键顺序一致的缓存值。</returns>
    private T[] ReadPhysicalBatch<T>(RedisClient client, string[] keys)
    {
        if (keys.Length == 1)
        {
            var rawSingle = client.Get(keys[0]);
            ValidateRawBatch(keys, new[] { rawSingle });
            return new[] { rawSingle == null ? default : (T)client.Deserialize(rawSingle, typeof(T)) };
        }

        var raw = CacheBatchAdaptive.Read(keys,
            batch => client.MGet(batch),
            (batch, values) => ValidateRawBatch(batch, values));
        var values = new T[raw.Length];
        for (var index = 0; index < raw.Length; index++)
            values[index] = raw[index] == null ? default : (T)client.Deserialize(raw[index], typeof(T));
        return values;
    }

    /// <summary>
    /// 异步批量读取并校验原始缓存值。
    /// </summary>
    /// <param name="client">执行读取的 Redis 客户端。</param>
    /// <param name="keys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入键顺序一致的原始缓存值。</returns>
    private async Task<string[]> ReadRawBatchAsync(RedisClient client, string[] keys,
        CancellationToken cancellationToken)
    {
        if (keys.Length == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawSingle = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                    () => client.GetAsync(keys[0]))
                .ConfigureAwait(false);
            var values = new[] { rawSingle };
            ValidateRawBatch(keys, values);
            return values;
        }

        return await CacheBatchAdaptive.ReadAsync(keys,
                batch => CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => client.MGetAsync(batch)),
                (batch, values) => ValidateRawBatch(batch, values),
                cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 校验批量读取结果数量和资源上限。
    /// </summary>
    /// <param name="keys">原始请求键。</param>
    /// <param name="values">Redis 返回的序列化值。</param>
    private void ValidateRawBatch(string[] keys, string[] values)
    {
        if (values == null)
            throw new InvalidOperationException($"Redis MGET 返回为空：请求 {keys.Length} 个键。");
        if (values.Length != keys.Length)
            throw new InvalidOperationException($"Redis MGET 返回数量与请求不一致：请求 {keys.Length} 个键，返回 {values.Length} 个值。");
        CacheBatchBudget.Check(values, _configuration.Resources.MaxBatchPayloadBytes);
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

        // 只枚举调用方输入一次；完整批次直接复用数组，尾批才复制精确长度。
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

        var physicalKeys = new string[_configuration.BatchSize];
        var logicalKeys = new string[_configuration.BatchSize];
        var count = 0;
        await foreach (var item in EnumerateMatchingKeysAsync(prefix, cancellationToken))
        {
            physicalKeys[count] = item.PhysicalKey;
            logicalKeys[count++] = item.LogicalKey;
            if (count < physicalKeys.Length)
                continue;

            var entries = await ReadPrefixBatchAsync<T>(physicalKeys, logicalKeys, cancellationToken)
                .ConfigureAwait(false);
            for (var index = 0; index < entries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return entries[index];
            }
            Array.Clear(physicalKeys, 0, count);
            Array.Clear(logicalKeys, 0, count);
            count = 0;
        }

        if (count == 0)
            yield break;
        var tail = await ReadPrefixBatchAsync<T>(CopyBatch(physicalKeys, count),
                CopyBatch(logicalKeys, count), cancellationToken)
            .ConfigureAwait(false);
        for (var index = 0; index < tail.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return tail[index];
        }
    }

    /// <summary>
    /// 按拓扑读取一批输入键并构造命中结果。
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

        // 单节点没有路由分组，直接复用物理键数组，避免构造 Dictionary、索引列表和复制批次。
        if (_configuration.Topology == CacheTopology.SingleNode)
        {
            var rawValues = await ReadRawBatchAsync(_client, physicalKeys, cancellationToken)
                .ConfigureAwait(false);
            FillEntries(_client, logicalKeys, entries, rawValues);
            return entries;
        }

        var groups = new Dictionary<RedisClient, List<int>>();
        for (var index = 0; index < count; index++)
        {
            var client = ClientForPhysicalKey(physicalKeys[index]);
            if (!groups.TryGetValue(client, out var group))
                groups.Add(client, group = new List<int>());
            group.Add(index);
        }
        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task<string[]>[windowCount];
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
                    tasks[dispatched] = ReadRawBatchAsync(group.Key, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < windowCount; groupIndex++)
                {
                    var group = grouped[offset + groupIndex];
                    FillEntries(group.Key, logicalKeys, entries, rawGroups[groupIndex], group.Value);
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        return entries;
    }

    /// <summary>
    /// 读取一批物理键对应的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入物理键顺序一致的缓存值。</returns>
    /// <remarks>供 List API 复用原始批量响应，避免为每个结果创建 <see cref="CacheEntry{T}"/>。</remarks>
    private async Task<T[]> ReadValuesBatchAsync<T>(IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        if (physicalKeys.Count == 0)
            return Array.Empty<T>();

        if (_configuration.Topology == CacheTopology.RedisCluster)
            return await ReadClusterValuesAsync<T>(physicalKeys, cancellationToken).ConfigureAwait(false);

        if (_configuration.Topology == CacheTopology.SingleNode)
        {
            var rawValues = await ReadRawBatchAsync(_client, GetBatchArray(physicalKeys), cancellationToken)
                .ConfigureAwait(false);
            return DeserializeValues<T>(_client, rawValues);
        }

        var values = new T[physicalKeys.Count];
        var groups = new Dictionary<RedisClient, List<int>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var client = ClientForPhysicalKey(physicalKeys[index]);
            if (!groups.TryGetValue(client, out var indexes))
                groups.Add(client, indexes = new List<int>());
            indexes.Add(index);
        }

        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task<string[]>[windowCount];
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
                    tasks[dispatched] = ReadRawBatchAsync(group.Key, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < windowCount; groupIndex++)
                {
                    var group = grouped[offset + groupIndex];
                    var decoded = DeserializeValues<T>(group.Key, rawGroups[groupIndex]);
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
    /// 按 Redis Cluster 槽位分组读取缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与输入物理键顺序一致的缓存值。</returns>
    private async Task<T[]> ReadClusterValuesAsync<T>(IList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        var groups = ClusterGroups(physicalKeys).ToArray();
        var values = new T[physicalKeys.Count];
        for (var offset = 0; offset < groups.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Length - offset);
            var tasks = new Task<string[]>[count];
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
                    tasks[dispatched] = batch.Length == 1
                        ? ReadSingleClusterValueAsync(batch[0], cancellationToken)
                        : ReadRawBatchAsync(_client, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var decoded = DeserializeValues<T>(_client, rawGroups[groupIndex]);
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
        IList<string> logicalKeys, CancellationToken cancellationToken)
    {
        var entries = new CacheEntry<T>[physicalKeys.Count];
        if (_configuration.Topology == CacheTopology.RedisCluster)
        {
            await FillClusterEntriesAsync(physicalKeys, logicalKeys, entries, cancellationToken)
                .ConfigureAwait(false);
            return entries;
        }

        // 扫描批次在单节点上无需再次按客户端分组；ToArray 只保留客户端要求的批数组。
        if (_configuration.Topology == CacheTopology.SingleNode)
        {
            var rawValues = await ReadRawBatchAsync(_client, GetBatchArray(physicalKeys), cancellationToken)
                .ConfigureAwait(false);
            FillEntries(_client, logicalKeys, entries, rawValues);
            return entries;
        }

        var groups = new Dictionary<RedisClient, List<int>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var client = ClientForPhysicalKey(physicalKeys[index]);
            if (!groups.TryGetValue(client, out var group))
                groups.Add(client, group = new List<int>());
            group.Add(index);
        }
        var grouped = groups.ToArray();
        for (var offset = 0; offset < grouped.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windowCount = Math.Min(_configuration.MaxConcurrency, grouped.Length - offset);
            var tasks = new Task<string[]>[windowCount];
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
                    tasks[dispatched] = ReadRawBatchAsync(group.Key, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < windowCount; groupIndex++)
                {
                    var group = grouped[offset + groupIndex];
                    FillEntries(group.Key, logicalKeys, entries, rawGroups[groupIndex], group.Value);
                }
            }
            catch
            {
                await ObservePendingAsync(tasks, dispatched).ConfigureAwait(false);
                throw;
            }
        }
        return entries;
    }

    /// <summary>
    /// 按 Cluster 槽位读取并填充缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKeys">物理键批次。</param>
    /// <param name="logicalKeys">对应的逻辑键批次。</param>
    /// <param name="entries">接收读取结果的数组。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task FillClusterEntriesAsync<T>(IList<string> physicalKeys, IList<string> logicalKeys,
        CacheEntry<T>[] entries, CancellationToken cancellationToken)
    {
        var groups = ClusterGroups(physicalKeys).ToArray();
        for (var offset = 0; offset < groups.Length; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Length - offset);
            var tasks = new Task<string[]>[count];
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
                    tasks[dispatched] = batch.Length == 1
                        ? ReadSingleClusterValueAsync(batch[0], cancellationToken)
                        : ReadRawBatchAsync(_client, batch, cancellationToken);
                }

                var rawGroups = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (var groupIndex = 0; groupIndex < count; groupIndex++)
                {
                    var group = groups[offset + groupIndex];
                    var batch = rawGroups[groupIndex];
                    if (batch.Length != group.Value.Count)
                        throw new InvalidOperationException($"Redis Cluster MGET 返回数量与请求不一致：槽位 {group.Key}，请求 {group.Value.Count}，返回 {batch.Length}。");
                    FillEntries(_client, logicalKeys, entries, batch, group.Value);
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
    /// 异步读取 Cluster 中的单个物理键。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含单个原始值的结果数组。</returns>
    private async Task<string[]> ReadSingleClusterValueAsync(string physicalKey,
        CancellationToken cancellationToken)
    {
        return await ReadRawBatchAsync(_client, new[] { physicalKey }, cancellationToken)
            .ConfigureAwait(false);
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
    /// <param name="count">复制元素数。</param>
    /// <returns>指定长度的数组。</returns>
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
    /// 将原始缓存值反序列化为指定类型。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="client">提供反序列化器的 Redis 客户端。</param>
    /// <param name="rawValues">原始序列化值。</param>
    /// <returns>与输入顺序一致的缓存值。</returns>
    private T[] DeserializeValues<T>(RedisClient client, string[] rawValues)
    {
        // rawValues 已由 ReadRawBatchAsync/ReadSingleClusterValueAsync 的 ValidateRawBatch 校验，
        // 解码热路径不再重复遍历完整响应；所有调用方必须继续从已校验的读取入口取得数组。
        var values = new T[rawValues.Length];
        var valueType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (client.Deserialize.Method.DeclaringType != typeof(JsonConvert) || JsonConvert.DefaultSettings != null ||
            !(valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(decimal)))
        {
            for (var index = 0; index < rawValues.Length; index++)
                values[index] = rawValues[index] == null
                    ? default
                    : (T)client.Deserialize(rawValues[index], typeof(T));
            return values;
        }

        // 只有默认标量 JSON 共用解析器；对象引用和转换器必须保留每个缓存值的独立上下文。
        // 非 nullable 值类型不能直接反序列化 JSON null，先用一次序列化的 default token 占位。
        var defaultValueJson = typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null
            ? JsonConvert.SerializeObject(default(T))
            : null;
        var payload = CacheJsonBatch.Build(rawValues, defaultValueJson);
        // 直接使用泛型数组，避免 Nullable<T> 反射数组和 Array.GetValue 的逐项装箱。
        values = (T[])client.Deserialize(payload, typeof(T[]));
        if (values == null || values.Length != rawValues.Length)
            throw new InvalidOperationException("Redis 批量反序列化结果与请求数量不一致。");
        return values;
    }

    /// <summary>
    /// 将批量响应映射为带命中状态的缓存条目。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="client">提供反序列化器的 Redis 客户端。</param>
    /// <param name="logicalKeys">逻辑键批次。</param>
    /// <param name="entries">接收结果的数组。</param>
    /// <param name="rawValues">原始序列化值。</param>
    /// <param name="indexes">可选的目标索引映射。</param>
    private void FillEntries<T>(RedisClient client, IList<string> logicalKeys,
        CacheEntry<T>[] entries, string[] rawValues, IList<int> indexes = null)
    {
        var values = DeserializeValues<T>(client, rawValues);
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
    /// 扫描并流式枚举前缀匹配的物理键和逻辑键。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配键的物理键和逻辑键。</returns>
    private async IAsyncEnumerable<(string PhysicalKey, string LogicalKey)> EnumerateMatchingKeysAsync(
        string prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topology = await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        foreach (var scanClient in _scanClients)
        {
            long cursor = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.ConsumePage();
                var page = await CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan,
                    () => scanClient.ScanAsync(cursor, scanPattern,
                    _configuration.BatchSize, "")).ConfigureAwait(false);
                cursor = page.cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.items)
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
        }
        CacheClusterTopology.EnsureUnchanged(topology,
            await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// 扫描并流式枚举前缀匹配的物理键。
    /// </summary>
    /// <param name="prefix">逻辑键前缀。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>匹配的物理键序列。</returns>
    private async IAsyncEnumerable<string> EnumerateMatchingPhysicalKeysAsync(string prefix,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var topology = await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false);
        using var hashContext = CacheKeyFormat.CreateHashContext(_configuration);
        var budget = new CacheScanBudget(_configuration.Resources);
        var encodedPrefix = CacheKeyFormat.GetEncodedLogicalPrefix(prefix);
        var scanPattern = CacheKeyFormat.GetPrefixPattern(_configuration, prefix);
        foreach (var scanClient in _scanClients)
        {
            long cursor = 0;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.ConsumePage();
                var page = await CacheCommandProbe.InvokeAsync(CacheCommandKind.Scan,
                    () => scanClient.ScanAsync(cursor, scanPattern,
                    _configuration.BatchSize, "")).ConfigureAwait(false);
                cursor = page.cursor;
                CacheCommandProbe.RecordScanPage(0);
                foreach (var physicalKey in page.items)
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
        }
        CacheClusterTopology.EnsureUnchanged(topology,
            await ValidateScanTopologyAsync(cancellationToken).ConfigureAwait(false));
    }
}
