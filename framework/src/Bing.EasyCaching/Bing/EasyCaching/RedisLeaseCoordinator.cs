using Bing.Caching;
using EasyCaching.Core;
using StackExchange.Redis;
using System.Diagnostics;

namespace Bing.EasyCaching;

/// <summary>
/// 在 EasyCaching Redis Provider 上协调回源和租约写入。
/// </summary>
internal sealed class RedisLeaseCoordinator
{
    /// <summary>
    /// 校验租约所有权并续期的 Lua 脚本。
    /// </summary>
    private const string RenewScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";
    /// <summary>
    /// 校验租约所有权并释放租约的 Lua 脚本。
    /// </summary>
    private const string ReleaseScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";
    /// <summary>
    /// 校验租约所有权并提交回源结果的 Lua 脚本。
    /// </summary>
    private const string CommitScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('SET', KEYS[2], ARGV[2], 'PX', ARGV[3]); redis.call('DEL', KEYS[1]); return 1 else return 0 end";

    /// <summary>
    /// 用于读写缓存值的 EasyCaching Provider。
    /// </summary>
    private readonly IEasyCachingProvider _provider;
    /// <summary>
    /// 当前缓存实例的配置。
    /// </summary>
    private readonly CacheConfiguration _configuration;
    /// <summary>
    /// Provider 对应的 Redis 数据库。
    /// </summary>
    private readonly IDatabase _database;
    /// <summary>
    /// 同步写入后执行的本地失效回调。
    /// </summary>
    private readonly Action<string> _invalidate;
    /// <summary>
    /// 异步写入后执行的本地失效回调。
    /// </summary>
    private readonly Func<string, CancellationToken, Task> _invalidateAsync;
    /// <summary>
    /// 批量同步写入后执行的本地失效回调。
    /// </summary>
    private readonly Action<IReadOnlyList<string>> _invalidateBatch;
    /// <summary>
    /// 批量异步写入后执行的本地失效回调。
    /// </summary>
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task> _invalidateBatchAsync;

    /// <summary>
    /// 初始化一个 <see cref="RedisLeaseCoordinator"/> 类型的实例。
    /// </summary>
    /// <param name="provider">EasyCaching Redis Provider。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="invalidate">单键同步失效回调。</param>
    /// <param name="invalidateAsync">单键异步失效回调。</param>
    /// <param name="invalidateBatch">批量同步失效回调。</param>
    /// <param name="invalidateBatchAsync">批量异步失效回调。</param>
    internal RedisLeaseCoordinator(IEasyCachingProvider provider, CacheConfiguration configuration,
        Action<string> invalidate = null, Func<string, CancellationToken, Task> invalidateAsync = null,
        Action<IReadOnlyList<string>> invalidateBatch = null,
        Func<IReadOnlyList<string>, CancellationToken, Task> invalidateBatchAsync = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _database = provider.Database as IDatabase ??
            throw new NotSupportedException("EasyCaching Redis Provider 未提供 StackExchange.Redis 数据库。");
        _invalidate = invalidate;
        _invalidateAsync = invalidateAsync;
        _invalidateBatch = invalidateBatch;
        _invalidateBatchAsync = invalidateBatchAsync;
    }

    /// <summary>
    /// 获取物理缓存键对应的租约键。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>租约键。</returns>
    private string LeaseKey(string physicalKey) => physicalKey + ":lease";

    /// <summary>
    /// 获取向上取整后的租约时长毫秒数。
    /// </summary>
    private long LeaseMilliseconds => (long)Math.Ceiling(_configuration.Coordination.LeaseDuration.TotalMilliseconds);

    /// <summary>
    /// 将有效期转换为向上取整的毫秒数。
    /// </summary>
    /// <param name="expiration">缓存有效期。</param>
    /// <returns>有效期毫秒数。</returns>
    private static long Milliseconds(TimeSpan expiration) => (long)Math.Ceiling(expiration.TotalMilliseconds);

    /// <summary>
    /// 按 Redis Cluster 槽位分组物理键。
    /// </summary>
    /// <param name="physicalKeys">待分组的物理键。</param>
    /// <returns>按槽位分组的键集合。</returns>
    private List<List<string>> GroupKeys(IReadOnlyList<string> physicalKeys)
    {
        if (_configuration.Topology != CacheTopology.RedisCluster)
            return new List<List<string>> { new List<string>(physicalKeys) };

        var groups = new Dictionary<int, List<string>>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var physicalKey = physicalKeys[index];
            var slot = CacheClusterTopology.GetHashSlot(physicalKey);
            if (!groups.TryGetValue(slot, out var group))
                groups.Add(slot, group = new List<string>());
            group.Add(physicalKey);
        }
        return groups.Values.ToList();
    }

    /// <summary>
    /// 按 Redis Cluster 槽位分组写入项。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">待分组的物理键和值。</param>
    /// <returns>按槽位分组的写入项。</returns>
    private List<List<KeyValuePair<string, T>>> GroupItems<T>(IReadOnlyList<KeyValuePair<string, T>> items)
    {
        if (_configuration.Topology != CacheTopology.RedisCluster)
            return new List<List<KeyValuePair<string, T>>> { new List<KeyValuePair<string, T>>(items) };

        var groups = new Dictionary<int, List<KeyValuePair<string, T>>>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var slot = CacheClusterTopology.GetHashSlot(item.Key);
            if (!groups.TryGetValue(slot, out var group))
                groups.Add(slot, group = new List<KeyValuePair<string, T>>());
            group.Add(item);
        }
        return groups.Values.ToList();
    }

    /// <summary>
    /// 创建用于删除数据键和租约键的 Redis 键数组。
    /// </summary>
    /// <param name="physicalKeys">数据物理键集合。</param>
    /// <returns>包含数据键和租约键的 Redis 键数组。</returns>
    private RedisKey[] CreateDeleteKeys(IReadOnlyList<string> physicalKeys)
    {
        var redisKeys = new RedisKey[physicalKeys.Count * 2];
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            redisKeys[index] = physicalKeys[index];
            redisKeys[physicalKeys.Count + index] = LeaseKey(physicalKeys[index]);
        }
        return redisKeys;
    }

    /// <summary>
    /// 按槽位和命令资源上限拆分删除批次。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理键。</param>
    /// <returns>符合当前命令预算的键批次。</returns>
    private IEnumerable<IReadOnlyList<string>> PartitionDeleteBatches(
        IReadOnlyList<string> physicalKeys)
    {
        foreach (var group in GroupKeys(physicalKeys))
        {
            foreach (var batch in CacheDeleteBatchBudget.Partition(group, _configuration.BatchSize,
                         _configuration.Resources.MaxBatchCommandBytes))
                yield return batch;
        }
    }

    /// <summary>
    /// 原子批量写入缓存值并清理对应租约的 Lua 脚本。
    /// </summary>
    private static readonly string BatchWriteScript =
        "local count=math.floor(#KEYS/2) " +
        "for i=1,count do " +
        "local offset=(i-1)*2 " +
        "redis.call('SET',KEYS[i],ARGV[offset+1],'PX',ARGV[offset+2]) " +
        "redis.call('DEL',KEYS[count+i]) " +
        "end return count";

    /// <summary>
    /// 序列化一组缓存写入值和有效期。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">待写入的物理键和值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <returns>供批量 Lua 脚本使用的 Redis 参数。</returns>
    private RedisValue[] CreateWriteValues<T>(IReadOnlyList<KeyValuePair<string, T>> items,
        TimeSpan expiration)
    {
        var values = new RedisValue[items.Count * 2];
        var milliseconds = Milliseconds(expiration);
        var serializer = _provider.GetProviderInfo().Serializer;
        for (var index = 0; index < items.Count; index++)
        {
            values[index * 2] = serializer.Serialize(items[index].Value);
            values[index * 2 + 1] = milliseconds;
        }
        return values;
    }

    /// <summary>
    /// 使批量操作涉及的本地缓存失效。
    /// </summary>
    /// <param name="physicalKeys">需要失效的物理键。</param>
    private void InvalidateBatch(IReadOnlyList<string> physicalKeys)
    {
        if (_invalidateBatch != null)
        {
            _invalidateBatch(physicalKeys);
            return;
        }
        if (_invalidate == null)
            return;
        for (var index = 0; index < physicalKeys.Count; index++)
            _invalidate(physicalKeys[index]);
    }

    /// <summary>
    /// 异步使批量操作涉及的本地缓存失效。
    /// </summary>
    /// <param name="physicalKeys">需要失效的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task InvalidateBatchAsync(IReadOnlyList<string> physicalKeys,
        CancellationToken cancellationToken)
    {
        if (_invalidateBatchAsync != null)
        {
            await _invalidateBatchAsync(physicalKeys, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (_invalidateAsync == null)
            return;
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _invalidateAsync(physicalKeys[index], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 使批量操作涉及的本地缓存失效。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">已写入的物理键和值。</param>
    private void InvalidateBatch<T>(IReadOnlyList<KeyValuePair<string, T>> items)
    {
        if (_invalidateBatch == null && _invalidate == null)
            return;
        var physicalKeys = new string[items.Count];
        for (var index = 0; index < items.Count; index++)
            physicalKeys[index] = items[index].Key;
        InvalidateBatch(physicalKeys);
    }

    /// <summary>
    /// 异步使批量操作涉及的本地缓存失效。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="items">已写入的物理键和值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private Task InvalidateBatchAsync<T>(IReadOnlyList<KeyValuePair<string, T>> items,
        CancellationToken cancellationToken)
    {
        if (_invalidateBatchAsync == null && _invalidateAsync == null)
            return Task.CompletedTask;
        var physicalKeys = new string[items.Count];
        for (var index = 0; index < items.Count; index++)
            physicalKeys[index] = items[index].Key;
        return InvalidateBatchAsync(physicalKeys, cancellationToken);
    }

    /// <summary>
    /// 写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待缓存的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    internal bool Write<T>(string physical, T value, TimeSpan expiration, bool onlyMissing = false)
    {
        var payload = _provider.GetProviderInfo().Serializer.Serialize(value);
        var script = onlyMissing
            ? "local r=redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2],'NX') if r then redis.call('DEL',KEYS[2]) end return r"
            : "local r=redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2]) redis.call('DEL',KEYS[2]) return r";
        var written = !_database.ScriptEvaluate(script, new RedisKey[] { physical, LeaseKey(physical) },
            new RedisValue[] { payload, Milliseconds(expiration) }).IsNull;
        if (written) _invalidate?.Invoke(physical);
        return written;
    }

    /// <summary>
    /// 异步写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待缓存的值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    internal async Task<bool> WriteAsync<T>(string physical, T value, TimeSpan expiration,
        bool onlyMissing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = _provider.GetProviderInfo().Serializer.Serialize(value);
        var script = onlyMissing
            ? "local r=redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2],'NX') if r then redis.call('DEL',KEYS[2]) end return r"
            : "local r=redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2]) redis.call('DEL',KEYS[2]) return r";
        var written = !(await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
            () => _database.ScriptEvaluateAsync(script,
                new RedisKey[] { physical, LeaseKey(physical) },
                new RedisValue[] { payload, Milliseconds(expiration) }))).IsNull;
        // Redis 写入已经返回后不能再由调用方取消跳过 Hybrid 失效；否则已提交值可能被本地旧值遮蔽。
        if (written && _invalidateAsync != null)
            await _invalidateAsync(physical, CancellationToken.None);
        return written;
    }

    /// <summary>
    /// 批量写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="values">物理键和值集合。</param>
    /// <param name="expiration">缓存有效期。</param>
    internal void WriteBatch<T>(IEnumerable<KeyValuePair<string, T>> values, TimeSpan expiration)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        var items = values as IReadOnlyList<KeyValuePair<string, T>> ?? values.ToList();
        if (items.Count == 0)
            return;

        foreach (var group in GroupItems(items))
        {
            var keys = new RedisKey[group.Count * 2];
            for (var index = 0; index < group.Count; index++)
            {
                keys[index] = group[index].Key;
                keys[group.Count + index] = LeaseKey(group[index].Key);
            }
            var result = _database.ScriptEvaluate(BatchWriteScript, keys, CreateWriteValues(group, expiration));
            if (Convert.ToInt64(result) != group.Count)
                throw new InvalidOperationException("Redis 批量协调写入返回数量不一致。");
            InvalidateBatch(group);
        }
    }

    /// <summary>
    /// 异步批量写入缓存值并清理对应租约。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="values">物理键和值集合。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal async Task WriteBatchAsync<T>(IEnumerable<KeyValuePair<string, T>> values, TimeSpan expiration,
        CancellationToken cancellationToken)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        var items = values as IReadOnlyList<KeyValuePair<string, T>> ?? values.ToList();
        if (items.Count == 0)
            return;

        var groups = GroupItems(items);
        for (var offset = 0; offset < groups.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Count - offset);
            var tasks = new Task[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks[dispatched] = WriteBatchGroupAsync(groups[offset + dispatched], expiration,
                        cancellationToken);
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
    /// 异步写入一个槽位分组并处理本地缓存失效。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="group">同一槽位的写入项。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task WriteBatchGroupAsync<T>(IReadOnlyList<KeyValuePair<string, T>> group,
        TimeSpan expiration, CancellationToken cancellationToken)
    {
        var keys = new RedisKey[group.Count * 2];
        for (var index = 0; index < group.Count; index++)
        {
            keys[index] = group[index].Key;
            keys[group.Count + index] = LeaseKey(group[index].Key);
        }
        var result = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
            () => _database.ScriptEvaluateAsync(BatchWriteScript, keys, CreateWriteValues(group, expiration)))
            .ConfigureAwait(false);
        if (Convert.ToInt64(result) != group.Count)
            throw new InvalidOperationException("Redis 批量协调写入返回数量不一致。");
        // 批量 Redis 写入已经提交后必须完成本地层和总线失效；取消只影响后续批次派发。
        await InvalidateBatchAsync(group, CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 删除缓存值及对应租约。
    /// </summary>
    /// <param name="physical">物理缓存键。</param>
    internal void Remove(string physical)
    {
        _database.KeyDelete(new RedisKey[] { physical, LeaseKey(physical) });
        _invalidate?.Invoke(physical);
    }

    /// <summary>
    /// 异步删除缓存值及对应租约。
    /// </summary>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal async Task RemoveAsync(string physical, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete,
            () => _database.KeyDeleteAsync(new RedisKey[] { physical, LeaseKey(physical) }));
        // 删除已经提交后不能跳过 Hybrid 失效通知。
        if (_invalidateAsync != null)
            await _invalidateAsync(physical, CancellationToken.None);
    }

    /// <summary>
    /// 批量删除缓存值及对应租约。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理缓存键。</param>
    internal void RemoveBatch(IList<string> physicalKeys)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (physicalKeys.Count == 0)
            return;

        var keys = physicalKeys as IReadOnlyList<string> ?? physicalKeys.ToArray();
        foreach (var batch in PartitionDeleteBatches(keys))
        {
            _database.KeyDelete(CreateDeleteKeys(batch));
            InvalidateBatch(batch);
        }
    }

    /// <summary>
    /// 异步批量删除缓存值及对应租约。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理缓存键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal async Task RemoveBatchAsync(IList<string> physicalKeys, CancellationToken cancellationToken)
    {
        if (physicalKeys == null)
            throw new ArgumentNullException(nameof(physicalKeys));
        if (physicalKeys.Count == 0)
            return;

        var keys = physicalKeys as IReadOnlyList<string> ?? physicalKeys.ToArray();
        var pending = new List<Task>(_configuration.MaxConcurrency);
        try
        {
            foreach (var batch in PartitionDeleteBatches(keys))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pending.Add(RemoveBatchGroupAsync(batch, cancellationToken));
                if (pending.Count < _configuration.MaxConcurrency)
                    continue;
                await Task.WhenAll(pending).ConfigureAwait(false);
                pending.Clear();
            }
            if (pending.Count > 0)
                await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            await ObservePendingAsync(pending.ToArray(), pending.Count).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 异步删除一组物理键并完成本地缓存失效。
    /// </summary>
    /// <param name="group">待删除的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RemoveBatchGroupAsync(IReadOnlyList<string> group,
        CancellationToken cancellationToken)
    {
        await CacheCommandProbe.InvokeAsync(CacheCommandKind.Delete,
            () => _database.KeyDeleteAsync(CreateDeleteKeys(group))).ConfigureAwait(false);
        // 删除已经提交后必须完成本地层和总线失效；调用方取消不回滚该命令。
        await InvalidateBatchAsync(group, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 等待已派发的异步操作结束。
    /// </summary>
    /// <param name="tasks">已派发的异步操作。</param>
    /// <param name="count">需要观察的任务数量。</param>
    private static async Task ObservePendingAsync(Task[] tasks, int count)
    {
        for (var index = 0; index < count; index++)
        {
            try { await tasks[index].ConfigureAwait(false); }
            catch { }
        }
    }

    /// <summary>
    /// 获取缓存值，并在未命中时协调执行回源。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="retrieve">同步回源委托。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <returns>缓存命中值或回源结果。</returns>
    internal T GetOrCreate<T>(string physicalKey, Func<T> retrieve, TimeSpan expiration)
    {
        var leaseKey = LeaseKey(physicalKey);
        var token = Guid.NewGuid().ToString("N");
        var wait = Stopwatch.StartNew();
        while (!_database.StringSet(leaseKey, token, _configuration.Coordination.LeaseDuration, When.NotExists))
        {
            var cached = _provider.Get<T>(physicalKey);
            if (cached.HasValue) return cached.Value;
            if (wait.Elapsed >= _configuration.Coordination.WaitTimeout)
                throw new TimeoutException("等待缓存回源租约超时。");
            Thread.Sleep(50 + (Environment.TickCount & 63));
        }

        var lost = 0;
        using var timeout = new CancellationTokenSource(_configuration.Coordination.RetrievalTimeout);
        using var renewal = new Timer(_ =>
        {
            try
            {
                if (timeout.IsCancellationRequested) return;
                if ((long)_database.ScriptEvaluate(RenewScript, new RedisKey[] { leaseKey },
                        new RedisValue[] { token, LeaseMilliseconds }) != 1)
                    Interlocked.Exchange(ref lost, 1);
            }
            catch { Interlocked.Exchange(ref lost, 1); }
        }, null, _configuration.Coordination.RenewalInterval, _configuration.Coordination.RenewalInterval);
        Exception primary = null;
        try
        {
            var cached = _provider.Get<T>(physicalKey);
            if (cached.HasValue) return cached.Value;
            CacheFactoryExecution.ThrowIfCancelled(timeout.Token, CancellationToken.None);
            var value = retrieve();
            if (timeout.IsCancellationRequested || Volatile.Read(ref lost) != 0)
                throw new TimeoutException("缓存回源已超时或失去租约。");
            Commit(leaseKey, physicalKey, token, value, expiration);
            _invalidate?.Invoke(physicalKey);
            return value;
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            renewal.Change(Timeout.Infinite, Timeout.Infinite);
            CacheFactoryExecution.Cleanup(() => _database.ScriptEvaluate(ReleaseScript,
                new RedisKey[] { leaseKey }, new RedisValue[] { token }), primary);
        }
    }

    /// <summary>
    /// 获取缓存值，并在未命中时协调执行回源。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="retrieve">接收取消令牌的异步回源委托。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存命中值或回源结果。</returns>
    internal async Task<T> GetOrCreateAsync<T>(string physicalKey, Func<CancellationToken, Task<T>> retrieve,
        TimeSpan expiration, CancellationToken cancellationToken)
    {
        var leaseKey = LeaseKey(physicalKey);
        var token = Guid.NewGuid().ToString("N");
        var wait = Stopwatch.StartNew();
        while (!await _database.StringSetAsync(leaseKey, token, _configuration.Coordination.LeaseDuration, When.NotExists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cached = await _provider.GetAsync<T>(physicalKey, cancellationToken);
            if (cached.HasValue) return cached.Value;
            if (wait.Elapsed >= _configuration.Coordination.WaitTimeout)
                throw new TimeoutException("等待缓存回源租约超时。");
            await Task.Delay(50 + (Environment.TickCount & 63), cancellationToken);
        }

        var lost = 0;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_configuration.Coordination.RetrievalTimeout);
        using var renewalStop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var renewal = RenewAsync(leaseKey, token, () => { Interlocked.Exchange(ref lost, 1); timeout.Cancel(); }, renewalStop.Token);
        Exception primary = null;
        try
        {
            var cached = await _provider.GetAsync<T>(physicalKey, cancellationToken);
            if (cached.HasValue) return cached.Value;
            var value = await CacheFactoryExecution.InvokeAsync(retrieve, timeout.Token, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref lost) != 0)
                throw new InvalidOperationException("缓存回源期间失去租约。");
            await CommitAsync(leaseKey, physicalKey, token, value, expiration);
            if (_invalidateAsync != null) await _invalidateAsync(physicalKey, cancellationToken);
            return value;
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            renewalStop.Cancel();
            await CacheFactoryExecution.CleanupAsync(async () =>
            {
                await renewal;
                await _database.ScriptEvaluateAsync(ReleaseScript, new RedisKey[] { leaseKey }, new RedisValue[] { token });
            }, primary, _configuration.Coordination.WaitTimeout);
        }
    }

    /// <summary>
    /// 在持有有效租约时提交回源结果。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="leaseKey">租约键。</param>
    /// <param name="key">数据物理键。</param>
    /// <param name="token">租约所有权令牌。</param>
    /// <param name="value">回源结果。</param>
    /// <param name="expiration">缓存有效期。</param>
    private void Commit<T>(string leaseKey, string key, string token, T value, TimeSpan expiration)
    {
        var payload = _provider.GetProviderInfo().Serializer.Serialize(value);
        if ((long)_database.ScriptEvaluate(CommitScript, new RedisKey[] { leaseKey, key },
                new RedisValue[] { token, payload, Milliseconds(expiration) }) != 1)
            throw new InvalidOperationException("缓存回源提交时已失去租约。");
    }

    /// <summary>
    /// 异步提交持有有效租约的回源结果。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="leaseKey">租约键。</param>
    /// <param name="key">数据物理键。</param>
    /// <param name="token">租约所有权令牌。</param>
    /// <param name="value">回源结果。</param>
    /// <param name="expiration">缓存有效期。</param>
    private async Task CommitAsync<T>(string leaseKey, string key, string token, T value, TimeSpan expiration)
    {
        var payload = _provider.GetProviderInfo().Serializer.Serialize(value);
        if ((long)await _database.ScriptEvaluateAsync(CommitScript, new RedisKey[] { leaseKey, key },
                new RedisValue[] { token, payload, Milliseconds(expiration) }) != 1)
            throw new InvalidOperationException("缓存回源提交时已失去租约。");
    }

    /// <summary>
    /// 定期续租并在失去所有权时通知调用方。
    /// </summary>
    /// <param name="leaseKey">租约键。</param>
    /// <param name="token">租约所有权令牌。</param>
    /// <param name="lost">租约失效时执行的回调。</param>
    /// <param name="cancellationToken">停止续租的取消令牌。</param>
    private async Task RenewAsync(string leaseKey, string token, Action lost, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_configuration.Coordination.RenewalInterval, cancellationToken);
                if ((long)await _database.ScriptEvaluateAsync(RenewScript, new RedisKey[] { leaseKey },
                        new RedisValue[] { token, LeaseMilliseconds }) != 1)
                {
                    lost();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { lost(); }
    }
}
