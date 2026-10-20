using System.Diagnostics;
using FreeRedis;

namespace Bing.Caching.FreeRedis;

/// <inheritdoc />
public partial class FreeRedisCacheManager
{
    /// <summary>
    /// 原子申请带过期时间的回源租约。
    /// </summary>
    private const string AcquireScript = "return redis.call('SET', KEYS[1], ARGV[1], 'NX', 'PX', ARGV[2])";
    /// <summary>
    /// 校验租约所有权后续期。
    /// </summary>
    private const string RenewScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";
    /// <summary>
    /// 校验租约所有权后释放租约。
    /// </summary>
    private const string ReleaseScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";
    /// <summary>
    /// 校验租约所有权并原子写回回源结果。
    /// </summary>
    private const string CommitScript = "if redis.call('GET', KEYS[1]) == ARGV[1] then redis.call('SET', KEYS[2], ARGV[2], 'PX', ARGV[3]); redis.call('DEL', KEYS[1]); return 1 else return 0 end";
    /// <summary>
    /// 覆盖缓存值并删除对应租约。
    /// </summary>
    private const string WriteScript = "local r = redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2]) redis.call('DEL', KEYS[2]) return r";
    /// <summary>
    /// 仅在缓存键不存在时写入并删除对应租约。
    /// </summary>
    private const string TryWriteScript = "local r = redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2], 'NX') if r then redis.call('DEL', KEYS[2]) end return r";

    /// <summary>
    /// 同步写入物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    private bool WritePhysical<T>(string physical, T value, TimeSpan expiration, bool onlyMissing = false)
    {
        var client = ClientForPhysicalKey(physical);
        return client.Eval(onlyMissing ? TryWriteScript : WriteScript, new[] { physical, physical + ":lease" },
            client.Serialize(value)?.ToString() ?? "null", Milliseconds(expiration)) != null;
    }

    /// <summary>
    /// 异步写入物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physical">物理缓存键。</param>
    /// <param name="value">待写入的缓存值。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="onlyMissing">是否仅在键不存在时写入。</param>
    /// <returns>写入成功时返回 <see langword="true"/>。</returns>
    private async Task<bool> WritePhysicalAsync<T>(string physical, T value, TimeSpan expiration, bool onlyMissing = false)
    {
        var client = ClientForPhysicalKey(physical);
        return await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleWrite,
                () => client.EvalAsync(onlyMissing ? TryWriteScript : WriteScript,
                    new[] { physical, physical + ":lease" },
                    client.Serialize(value)?.ToString() ?? "null", Milliseconds(expiration)))
            .ConfigureAwait(false) != null;
    }

    /// <summary>
    /// 表示路由到同一 Redis 客户端的待删除物理键集合。
    /// </summary>
    private sealed class PhysicalDeleteGroup
    {
        /// <summary>
        /// 执行删除命令的 Redis 客户端。
        /// </summary>
        internal readonly RedisClient Client;
        /// <summary>
        /// 当前拓扑分组中的物理键。
        /// </summary>
        internal readonly List<string> PhysicalKeys;

        /// <summary>
        /// 初始化一个 <see cref="PhysicalDeleteGroup"/> 类型的实例。
        /// </summary>
        /// <param name="client">执行删除命令的客户端。</param>
        internal PhysicalDeleteGroup(RedisClient client)
        {
            Client = client;
            PhysicalKeys = new List<string>();
        }
    }

    /// <summary>
    /// 表示按命令预算拆分后的物理键删除操作。
    /// </summary>
    private sealed class PhysicalDeleteCommand
    {
        /// <summary>
        /// 执行删除命令的 Redis 客户端。
        /// </summary>
        internal readonly RedisClient Client;
        /// <summary>
        /// 本条删除命令覆盖的物理键。
        /// </summary>
        internal readonly IReadOnlyList<string> PhysicalKeys;

        /// <summary>
        /// 初始化一个 <see cref="PhysicalDeleteCommand"/> 类型的实例。
        /// </summary>
        /// <param name="client">执行删除命令的客户端。</param>
        /// <param name="physicalKeys">命令覆盖的物理键。</param>
        internal PhysicalDeleteCommand(RedisClient client, IReadOnlyList<string> physicalKeys)
        {
            Client = client;
            PhysicalKeys = physicalKeys;
        }
    }

    /// <summary>
    /// 按 Redis 槽位或客户端分片对物理键分组。
    /// </summary>
    /// <param name="physicalKeys">待分组的物理键。</param>
    /// <returns>按目标客户端组织的删除分组。</returns>
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

        var clientGroups = new Dictionary<RedisClient, PhysicalDeleteGroup>();
        for (var index = 0; index < physicalKeys.Count; index++)
        {
            var physicalKey = physicalKeys[index];
            var client = ClientForPhysicalKey(physicalKey);
            if (!clientGroups.TryGetValue(client, out var group))
                clientGroups.Add(client, group = new PhysicalDeleteGroup(client));
            group.PhysicalKeys.Add(physicalKey);
        }
        return clientGroups.Values.ToList();
    }

    /// <summary>
    /// 初始化删除缓存键和租约键所需的参数。
    /// </summary>
    /// <param name="physicalKeys">缓存数据物理键。</param>
    /// <returns>Redis 删除命令参数数组。</returns>
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
    /// 按拓扑及资源限制拆分物理删除命令。
    /// </summary>
    /// <param name="physicalKeys">待删除的物理键。</param>
    /// <returns>拆分后的删除命令。</returns>
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
    /// 同步删除一批物理缓存键及租约键。
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
    /// 异步删除一批物理缓存键及租约键。
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
    /// 保护本地回源门闩集合的同步对象。
    /// </summary>
    private readonly object _gateSync = new();
    /// <summary>
    /// 按物理缓存键保存进程内回源门闩。
    /// </summary>
    private readonly Dictionary<string, LocalGate> _gates = new();
    /// <summary>
    /// 保存当前异步执行流中的回源键链，用于检测同键递归。
    /// </summary>
    private readonly AsyncLocal<ActiveKey> _activeKey = new();

    /// <summary>
    /// 表示用于合并同键回源的进程内门闩。
    /// </summary>
    private sealed class LocalGate
    {
        /// <summary>
        /// 串行化相同物理键的本地回源操作。
        /// </summary>
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
        /// <summary>
        /// 当前引用该门闩的调用数量。
        /// </summary>
        internal int Users;
    }

    /// <summary>
    /// 表示异步执行流中的一个回源键节点。
    /// </summary>
    private sealed class ActiveKey
    {
        /// <summary>
        /// 当前回源操作对应的物理键。
        /// </summary>
        internal string Key;
        /// <summary>
        /// 执行流中前一个回源键节点。
        /// </summary>
        internal ActiveKey Parent;
    }

    /// <summary>
    /// 保存回源期间的租约状态。
    /// </summary>
    private sealed class LeaseState
    {
        /// <summary>
        /// 租约丢失标记；非零表示当前回源不得提交结果。
        /// </summary>
        internal int Lost;
    }

    /// <summary>
    /// 获取物理键对应的进程内回源门闩并增加引用计数。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>对应的本地门闩。</returns>
    private LocalGate AddGate(string physicalKey)
    {
        lock (_gateSync)
        {
            if (!_gates.TryGetValue(physicalKey, out var gate))
            {
                gate = new LocalGate();
                _gates.Add(physicalKey, gate);
            }
            gate.Users++;
            return gate;
        }
    }

    /// <summary>
    /// 释放物理键对应的进程内回源门闩。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="gate">待释放的门闩。</param>
    /// <param name="entered">调用方是否已进入门闩。</param>
    private void RemoveGate(string physicalKey, LocalGate gate, bool entered)
    {
        if (entered)
            gate.Semaphore.Release();
        lock (_gateSync)
        {
            if (--gate.Users == 0)
            {
                _gates.Remove(physicalKey);
                gate.Semaphore.Dispose();
            }
        }
    }

    /// <summary>
    /// 检查当前执行流是否递归请求同一个缓存键。
    /// </summary>
    /// <param name="physicalKey">待检查的物理缓存键。</param>
    private void CheckRecursion(string physicalKey)
    {
        for (var current = _activeKey.Value; current != null; current = current.Parent)
            if (current.Key == physicalKey)
                throw new InvalidOperationException("缓存回源不能递归请求同一个键。");
    }

    /// <summary>
    /// 同步读取缓存值并返回命中状态。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="hit">输出是否命中缓存。</param>
    /// <returns>缓存值；未命中时为类型默认值。</returns>
    private T ReadCached<T>(string physicalKey, out bool hit)
    {
        var raw = ClientForPhysicalKey(physicalKey).Get(physicalKey);
        hit = raw != null;
        return hit ? (T)ClientForPhysicalKey(physicalKey).Deserialize(raw, typeof(T)) : default;
    }

    /// <summary>
    /// 异步读取缓存值并返回命中状态。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <returns>命中状态及缓存值。</returns>
    private async Task<(bool Hit, T Value)> ReadCachedAsync<T>(string physicalKey)
    {
        var raw = await CacheCommandProbe.InvokeAsync(CacheCommandKind.SingleRead,
                () => ClientForPhysicalKey(physicalKey).GetAsync(physicalKey))
            .ConfigureAwait(false);
        return raw == null ? (false, default) : (true, (T)ClientForPhysicalKey(physicalKey).Deserialize(raw, typeof(T)));
    }

    /// <summary>
    /// 将有效期向上取整为 Redis 使用的毫秒数。
    /// </summary>
    /// <param name="value">缓存有效期。</param>
    /// <returns>有效期毫秒数。</returns>
    private static long Milliseconds(TimeSpan value) => (long)Math.Ceiling(value.TotalMilliseconds);

    /// <summary>
    /// 协调同步回源并在成功后写回缓存。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="retriever">未命中时调用的回源委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <returns>缓存命中值或回源结果。</returns>
    private T GetOrCreate<T>(string key, Func<T> retriever, CacheOptions options)
    {
        var physicalKey = PhysicalKey(key);
        var client = ClientForPhysicalKey(physicalKey);
        CheckRecursion(physicalKey);
        var cached = ReadCached<T>(physicalKey, out var hit);
        if (hit)
            return cached;

        var gate = AddGate(physicalKey);
        var entered = false;
        try
        {
            if (!gate.Semaphore.Wait(_configuration.Coordination.WaitTimeout))
                throw new TimeoutException("等待本地缓存回源协调超时。");
            entered = true;
            cached = ReadCached<T>(physicalKey, out hit);
            if (hit)
                return cached;
            using var admission = CacheFactoryAdmission.Enter(_configuration.Resources, _configuration.Coordination.WaitTimeout);
            var leaseKey = CacheKeyFormat.GetLeaseKey(_configuration, key);
            var token = Guid.NewGuid().ToString("N");
            var wait = Stopwatch.StartNew();
            while (client.Eval(AcquireScript, new[] { leaseKey }, token,
                       Milliseconds(_configuration.Coordination.LeaseDuration)) == null)
            {
                cached = ReadCached<T>(physicalKey, out hit);
                if (hit)
                    return cached;
                if (wait.Elapsed >= _configuration.Coordination.WaitTimeout)
                    throw new TimeoutException("等待缓存回源租约超时。");
                Thread.Sleep(50 + (Environment.TickCount & 63));
            }

            var state = new LeaseState();
            using var timeout = new CancellationTokenSource(_configuration.Coordination.RetrievalTimeout);
            using var renewal = new Timer(_ =>
            {
                try
                {
                    if (timeout.IsCancellationRequested) return;
                    if (Convert.ToInt64(client.Eval(RenewScript, new[] { leaseKey }, token,
                            Milliseconds(_configuration.Coordination.LeaseDuration))) != 1)
                        Interlocked.Exchange(ref state.Lost, 1);
                }
                catch { Interlocked.Exchange(ref state.Lost, 1); }
            }, null, _configuration.Coordination.RenewalInterval,
                _configuration.Coordination.RenewalInterval);
            var previous = _activeKey.Value;
            _activeKey.Value = new ActiveKey { Key = physicalKey, Parent = previous };
            Exception primary = null;
            try
            {
                cached = ReadCached<T>(physicalKey, out hit);
                if (hit)
                    return cached;
                var expiration = GetExpiration(options);
                CacheFactoryExecution.ThrowIfCancelled(timeout.Token, CancellationToken.None);
                var result = retriever();
                if (timeout.IsCancellationRequested || Volatile.Read(ref state.Lost) != 0)
                    throw new TimeoutException("缓存回源已超时或失去租约。");
                var serialized = client.Serialize(result)?.ToString() ?? "null";
                if (Convert.ToInt64(client.Eval(CommitScript, new[] { leaseKey, physicalKey }, token,
                        serialized, Milliseconds(expiration))) != 1)
                    throw new InvalidOperationException("缓存回源提交时已失去租约。");
                return result;
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                _activeKey.Value = previous;
                renewal.Change(Timeout.Infinite, Timeout.Infinite);
                CacheFactoryExecution.Cleanup(() => client.Eval(ReleaseScript, new[] { leaseKey }, token), primary);
            }
        }
        finally { RemoveGate(physicalKey, gate, entered); }
    }

    /// <summary>
    /// 协调异步回源并在成功后写回缓存。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="retriever">接收取消令牌的回源委托。</param>
    /// <param name="options">缓存选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>缓存命中值或回源结果。</returns>
    private async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> retriever,
        CacheOptions options, CancellationToken cancellationToken)
    {
        var physicalKey = PhysicalKey(key);
        var client = ClientForPhysicalKey(physicalKey);
        CheckRecursion(physicalKey);
        cancellationToken.ThrowIfCancellationRequested();
        var cached = await ReadCachedAsync<T>(physicalKey);
        if (cached.Hit)
            return cached.Value;

        var gate = AddGate(physicalKey);
        var entered = false;
        try
        {
            if (!await gate.Semaphore.WaitAsync(_configuration.Coordination.WaitTimeout, cancellationToken))
                throw new TimeoutException("等待本地缓存回源协调超时。");
            entered = true;
            cached = await ReadCachedAsync<T>(physicalKey);
            if (cached.Hit)
                return cached.Value;
            using var admission = await CacheFactoryAdmission.EnterAsync(_configuration.Resources, _configuration.Coordination.WaitTimeout, cancellationToken);
            var leaseKey = CacheKeyFormat.GetLeaseKey(_configuration, key);
            var token = Guid.NewGuid().ToString("N");
            var wait = Stopwatch.StartNew();
            while (await client.EvalAsync(AcquireScript, new[] { leaseKey }, token,
                       Milliseconds(_configuration.Coordination.LeaseDuration)) == null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cached = await ReadCachedAsync<T>(physicalKey);
                if (cached.Hit)
                    return cached.Value;
                if (wait.Elapsed >= _configuration.Coordination.WaitTimeout)
                    throw new TimeoutException("等待缓存回源租约超时。");
                await Task.Delay(50 + (Environment.TickCount & 63), cancellationToken);
            }

            var state = new LeaseState();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_configuration.Coordination.RetrievalTimeout);
            using var stopRenewal = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var renewal = RenewAsync(client, leaseKey, token, state, stopRenewal.Token, timeout);
            var previous = _activeKey.Value;
            _activeKey.Value = new ActiveKey { Key = physicalKey, Parent = previous };
            Exception primary = null;
            try
            {
                cached = await ReadCachedAsync<T>(physicalKey);
                if (cached.Hit)
                    return cached.Value;
                var expiration = GetExpiration(options);
                var result = await CacheFactoryExecution.InvokeAsync(retriever, timeout.Token, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref state.Lost) != 0)
                    throw new InvalidOperationException("缓存回源期间失去租约。");
                var serialized = client.Serialize(result)?.ToString() ?? "null";
                if (Convert.ToInt64(await client.EvalAsync(CommitScript, new[] { leaseKey, physicalKey },
                        token, serialized, Milliseconds(expiration))) != 1)
                    throw new InvalidOperationException("缓存回源提交时已失去租约。");
                return result;
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                _activeKey.Value = previous;
                stopRenewal.Cancel();
                await CacheFactoryExecution.CleanupAsync(async () =>
                {
                    await renewal;
                    await client.EvalAsync(ReleaseScript, new[] { leaseKey }, token);
                }, primary, _configuration.Coordination.WaitTimeout);
            }
        }
        finally { RemoveGate(physicalKey, gate, entered); }
    }

    /// <summary>
    /// 定期续租并在租约失效时取消当前回源操作。
    /// </summary>
    /// <param name="client">执行租约命令的 Redis 客户端。</param>
    /// <param name="leaseKey">租约物理键。</param>
    /// <param name="token">租约所有权令牌。</param>
    /// <param name="state">接收租约状态的对象。</param>
    /// <param name="cancellationToken">停止续租的取消令牌。</param>
    /// <param name="lifetime">回源操作生命周期令牌源。</param>
    private async Task RenewAsync(global::FreeRedis.RedisClient client, string leaseKey, string token, LeaseState state,
        CancellationToken cancellationToken, CancellationTokenSource lifetime)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_configuration.Coordination.RenewalInterval, cancellationToken);
                if (Convert.ToInt64(await client.EvalAsync(RenewScript, new[] { leaseKey }, token,
                        Milliseconds(_configuration.Coordination.LeaseDuration))) != 1)
                {
                    Interlocked.Exchange(ref state.Lost, 1);
                    lifetime.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch { Interlocked.Exchange(ref state.Lost, 1); lifetime.Cancel(); }
    }
}
