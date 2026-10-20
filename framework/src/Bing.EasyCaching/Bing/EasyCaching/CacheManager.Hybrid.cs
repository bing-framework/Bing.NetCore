using Bing.Caching;
using EasyCaching.Core;
using StackExchange.Redis;
using System.Diagnostics;

namespace Bing.EasyCaching;

/// <inheritdoc />
public partial class CacheManager
{
    // 值和剩余 TTL 必须来自同一次原子读取，不能把旧值配上并发新写入的 TTL。
    /// <summary>
    /// 在同一 Redis 脚本中读取缓存值和剩余有效期。
    /// </summary>
    private const string ReadWithTtlScript = "local r = {} for i,k in ipairs(KEYS) do " +
        "r[#r+1] = redis.call('GET', k) or false r[#r+1] = redis.call('PTTL', k) end return r";

    /// <summary>
    /// 批量读取 Hybrid 分布式层并回填本地层。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <returns>按物理键索引的缓存值和命中状态。</returns>
    private IDictionary<string, CacheValue<T>> ReadHybridBatch<T>(IList<string> keys)
    {
        var database = _cachingProvider.Database as IDatabase;
        if (database == null)
            throw new NotSupportedException("Hybrid 批量回填需要支持原子值/TTL 读取的 Redis Provider。");
        var values = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        if (!IsCluster)
        {
            var singleNodeKeys = keys as string[] ?? keys.ToArray();
            var generation = CaptureHybridGeneration();
            var clock = Stopwatch.StartNew();
            var raw = ReadHybridRawBatch(database, singleNodeKeys);
            FillHybridBatch<T>(singleNodeKeys, raw, values, clock,
                (key, value, ttl) => _hybridRefillGeneration.TryApply(generation,
                    () => _localProvider.Set(key, value, ttl)),
                key => _hybridRefillGeneration.TryApply(generation, () => _localProvider.Remove(key)));
            return values;
        }

        var groups = GroupPhysicalKeysBySlot(keys);
        foreach (var group in groups)
        {
            var groupedKeys = group.ToArray();
            var generation = CaptureHybridGeneration();
            var clock = Stopwatch.StartNew();
            var raw = ReadHybridRawBatch(database, groupedKeys);
            FillHybridBatch<T>(group, raw, values, clock,
                (key, value, ttl) => _hybridRefillGeneration.TryApply(generation,
                    () => _localProvider.Set(key, value, ttl)),
                key => _hybridRefillGeneration.TryApply(generation, () => _localProvider.Remove(key)));
        }
        return values;
    }

    /// <summary>
    /// 异步批量读取 Hybrid 分布式层并回填本地层。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按物理键索引的缓存值和命中状态。</returns>
    private async Task<IDictionary<string, CacheValue<T>>> ReadHybridBatchAsync<T>(IList<string> keys,
        CancellationToken cancellationToken)
    {
        var database = _cachingProvider.Database as IDatabase;
        if (database == null)
            throw new NotSupportedException("Hybrid 批量回填需要支持原子值/TTL 读取的 Redis Provider。");
        var values = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        if (!IsCluster)
        {
            var singleNodeKeys = keys as string[] ?? keys.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            var generation = CaptureHybridGeneration();
            var clock = Stopwatch.StartNew();
            var raw = await ReadHybridRawBatchAsync(database, singleNodeKeys, cancellationToken)
                .ConfigureAwait(false);
            var local = new List<(string Key, T Value, TimeSpan Ttl)>();
            var missing = new List<string>();
            FillHybridBatch<T>(singleNodeKeys, raw, values, clock,
                (key, value, ttl) => local.Add((key, value, clock.Elapsed + ttl)),
                key => missing.Add(key));
            await ApplyHybridRefillAsync(local, missing, generation, clock, cancellationToken)
                .ConfigureAwait(false);
            return values;
        }

        var groups = GroupPhysicalKeysBySlot(keys);
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var groupedKeys = group.ToArray();
            var generation = CaptureHybridGeneration();
            var clock = Stopwatch.StartNew();
            var raw = await ReadHybridRawBatchAsync(database, groupedKeys, cancellationToken).ConfigureAwait(false);
            var local = new List<(string Key, T Value, TimeSpan Ttl)>();
            var missing = new List<string>();
            FillHybridBatch<T>(group, raw, values, clock,
                (key, value, ttl) => local.Add((key, value, clock.Elapsed + ttl)), key => missing.Add(key));
            await ApplyHybridRefillAsync(local, missing, generation, clock, cancellationToken)
                .ConfigureAwait(false);
        }
        return values;
    }

    /// <summary>
    /// 按读取时的 Hybrid 代次更新本地缓存层。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="local">需要写入本地层的缓存项及有效期。</param>
    /// <param name="missing">需要从本地层清除的物理键。</param>
    /// <param name="generation">分布式读取开始时捕获的代次。</param>
    /// <param name="clock">用于扣除读取耗时的计时器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task ApplyHybridRefillAsync<T>(IList<(string Key, T Value, TimeSpan Ttl)> local,
        IList<string> missing, long generation, Stopwatch clock, CancellationToken cancellationToken)
    {
        foreach (var item in local)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = item.Ttl - clock.Elapsed;
            if (remaining > TimeSpan.Zero)
                await _hybridRefillGeneration.TryApplyAsync(generation,
                    () => _localProvider.SetAsync(item.Key, item.Value, remaining, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            else
                await _hybridRefillGeneration.TryApplyAsync(generation,
                    () => _localProvider.RemoveAsync(item.Key, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
        }
        foreach (var key in missing)
            await _hybridRefillGeneration.TryApplyAsync(generation,
                () => _localProvider.RemoveAsync(key, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
    }

    /// <summary>
    /// 原子批量读取 Redis 缓存值及剩余有效期。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="keys">物理缓存键数组。</param>
    /// <returns>按键顺序交替包含值和剩余有效期的响应。</returns>
    private RedisResult[] ReadHybridRawBatch(IDatabase database, string[] keys) =>
        CacheBatchAdaptive.Read(keys,
            batch => (RedisResult[])database.ScriptEvaluate(ReadWithTtlScript,
                ToRedisKeys(batch)),
            ValidateHybridRawBatch);

    /// <summary>
    /// 异步原子批量读取 Redis 缓存值及剩余有效期。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="keys">物理缓存键数组。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按键顺序交替包含值和剩余有效期的响应。</returns>
    private async Task<RedisResult[]> ReadHybridRawBatchAsync(IDatabase database, string[] keys,
        CancellationToken cancellationToken) =>
        await CacheBatchAdaptive.ReadAsync(keys,
            batch => CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, async () =>
                (RedisResult[])await database.ScriptEvaluateAsync(ReadWithTtlScript,
                    ToRedisKeys(batch))),
            ValidateHybridRawBatch,
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 校验 Redis 值与有效期批量响应及其资源占用。
    /// </summary>
    /// <param name="keys">原始请求键。</param>
    /// <param name="raw">Redis 返回的值和有效期。</param>
    private void ValidateHybridRawBatch(string[] keys, RedisResult[] raw)
    {
        if (raw == null || raw.Length != keys.Length * 2)
            throw new InvalidOperationException("Redis 值/TTL 批量响应数量不一致。");
        CacheBatchBudget.Check(raw, value => (byte[])value,
            _configuration.Resources.MaxBatchPayloadBytes);
    }

    /// <summary>
    /// 解码 Hybrid 批量响应并记录本地层回填或清理操作。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <param name="raw">Redis 值与有效期响应。</param>
    /// <param name="values">接收分布式缓存值的字典。</param>
    /// <param name="clock">用于扣除读取耗时的计时器。</param>
    /// <param name="refill">记录本地层回填操作的委托。</param>
    /// <param name="remove">记录本地层清理操作的委托。</param>
    private void FillHybridBatch<T>(IList<string> keys, RedisResult[] raw,
        IDictionary<string, CacheValue<T>> values, Stopwatch clock,
        Action<string, T, TimeSpan> refill, Action<string> remove)
    {
        if (raw == null || raw.Length != keys.Count * 2)
            throw new InvalidOperationException("Redis 值/TTL 批量响应数量不一致。");
        // ReadHybridRawBatch 已在每个原始响应子批次上校验预算；拆分后的合并数组可能再次超过总预算，不能重复按整批拒绝。
        var serializer = _cachingProvider.GetProviderInfo().Serializer;
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (raw[index * 2].IsNull)
            {
                values[key] = new CacheValue<T>(default, false);
                remove(key);
                continue;
            }
            var value = serializer.Deserialize<T>((byte[])raw[index * 2]);
            values[key] = new CacheValue<T>(value, true);
            // EasyCaching 内存 Provider 拒绝写入 null；null 仍是分布式层的命中值，清除本地旧值即可保留 Hit 语义。
            if (ReferenceEquals(value, null))
            {
                remove(key);
                continue;
            }
            // 向下取整，额外预留 1ms；不为永久或已到期的值推断默认 TTL。
            var remainingMs = (long)raw[index * 2 + 1] - (long)Math.Ceiling(clock.Elapsed.TotalMilliseconds) - 1;
            if (remainingMs > 0)
                refill(key, value, TimeSpan.FromMilliseconds(remainingMs));
            else
                remove(key);
        }
    }

    /// <summary>
    /// 按当前拓扑批量读取 Redis 缓存值并解码。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <returns>按物理键索引的缓存值及命中状态。</returns>
    private IDictionary<string, CacheValue<T>> ReadRawRedisBatch<T>(IList<string> keys)
    {
        var database = (IDatabase)_cachingProvider.Database;
        var result = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        if (!IsCluster)
        {
            var singleNodeKeys = keys as string[] ?? keys.ToArray();
            DecodeRawRedisBatch(singleNodeKeys, ReadRawRedisValues(database, singleNodeKeys), result);
            return result;
        }

        var groups = GroupPhysicalKeysBySlot(keys);
        foreach (var group in groups)
        {
            var groupedKeys = group.ToArray();
            DecodeRawRedisBatch(group, ReadRawRedisValues(database, groupedKeys), result);
        }
        return result;
    }

    /// <summary>
    /// 异步按当前拓扑批量读取 Redis 缓存值并解码。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>按物理键索引的缓存值及命中状态。</returns>
    private async Task<IDictionary<string, CacheValue<T>>> ReadRawRedisBatchAsync<T>(IList<string> keys,
        CancellationToken token)
    {
        var database = (IDatabase)_cachingProvider.Database;
        var result = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        if (!IsCluster)
        {
            var singleNodeKeys = keys as string[] ?? keys.ToArray();
            var response = await ReadRawRedisValuesAsync(database, singleNodeKeys, token).ConfigureAwait(false);
            DecodeRawRedisBatch(singleNodeKeys, response, result);
            return result;
        }

        var groups = GroupPhysicalKeysBySlot(keys);
        for (var offset = 0; offset < groups.Count; offset += _configuration.MaxConcurrency)
        {
            var count = Math.Min(_configuration.MaxConcurrency, groups.Count - offset);
            var tasks = new Task<RedisValue[]>[count];
            var groupedKeys = new string[count][];
            try
            {
                for (var index = 0; index < count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    groupedKeys[index] = groups[offset + index].ToArray();
                    tasks[index] = ReadRawRedisValuesAsync(database, groupedKeys[index], token);
                }
                var responses = await Task.WhenAll(tasks);
                for (var index = 0; index < responses.Length; index++)
                    DecodeRawRedisBatch(groupedKeys[index], responses[index], result);
            }
            catch
            {
                foreach (var task in tasks)
                    if (task != null)
                        CacheFactoryExecution.Observe(task);
                throw;
            }
        }
        return result;
    }

    /// <summary>
    /// 批量读取 Redis 原始缓存值。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="keys">物理缓存键数组。</param>
    /// <returns>与请求键顺序一致的原始值。</returns>
    private RedisValue[] ReadRawRedisValues(IDatabase database, string[] keys) =>
        CacheBatchAdaptive.Read(keys,
            batch => database.StringGet(ToRedisKeys(batch)),
            ValidateRawRedisValues);

    /// <summary>
    /// 异步批量读取 Redis 原始缓存值。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="keys">物理缓存键数组。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>与请求键顺序一致的原始值。</returns>
    private async Task<RedisValue[]> ReadRawRedisValuesAsync(IDatabase database, string[] keys,
        CancellationToken cancellationToken) =>
        await CacheBatchAdaptive.ReadAsync(keys,
            batch => CacheCommandProbe.InvokeAsync(CacheCommandKind.BatchRead, () => database.StringGetAsync(
                ToRedisKeys(batch))),
            ValidateRawRedisValues,
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 校验 Redis 批量读取结果数量和资源占用。
    /// </summary>
    /// <param name="keys">原始请求键。</param>
    /// <param name="values">Redis 返回的原始值。</param>
    private void ValidateRawRedisValues(string[] keys, RedisValue[] values)
    {
        if (values == null || values.Length != keys.Length)
            throw new InvalidOperationException("Redis 批量返回数量不一致。");
        CacheBatchBudget.Check(values, value => (byte[])value,
            _configuration.Resources.MaxBatchPayloadBytes);
    }

    /// <summary>
    /// 将 Redis 原始值解码为带命中状态的缓存值。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理缓存键。</param>
    /// <param name="values">Redis 原始值。</param>
    /// <param name="result">接收解码结果的字典。</param>
    private void DecodeRawRedisBatch<T>(IList<string> keys, RedisValue[] values,
        IDictionary<string, CacheValue<T>> result)
    {
        if (values.Length != keys.Count) throw new InvalidOperationException("Redis 批量返回数量不一致。");
        // ReadRawRedisValues 已在每个原始响应子批次上校验预算；拆分后的合并数组不能再次按整批拒绝。
        var serializer = _cachingProvider.GetProviderInfo().Serializer;
        for (var index = 0; index < values.Length; index++)
        {
            var raw = (byte[])values[index];
            result[keys[index]] = raw == null ? new CacheValue<T>(default, false)
                : new CacheValue<T>(serializer.Deserialize<T>(raw), true);
        }
    }

    /// <summary>
    /// 将字符串物理键转换为 Redis 键数组。
    /// </summary>
    /// <param name="keys">物理键列表。</param>
    /// <returns>Redis 键数组。</returns>
    private static RedisKey[] ToRedisKeys(IList<string> keys)
    {
        var result = new RedisKey[keys.Count];
        for (var index = 0; index < result.Length; index++)
            result[index] = keys[index];
        return result;
    }

}
