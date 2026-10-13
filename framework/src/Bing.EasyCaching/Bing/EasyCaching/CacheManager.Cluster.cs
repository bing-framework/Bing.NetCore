using Bing.Caching;
using EasyCaching.Core;
using StackExchange.Redis;

namespace Bing.EasyCaching;

/// <inheritdoc />
public partial class CacheManager
{
    /// <summary>
    /// 获取当前 Provider 是否使用 Redis Cluster 拓扑。
    /// </summary>
    private bool IsCluster => _configuration.Topology == CacheTopology.RedisCluster;

    /// <summary>
    /// 按 Redis 哈希槽对物理键分组。
    /// </summary>
    /// <param name="keys">物理键列表。</param>
    /// <returns>每项均由同一槽位键组成的分组。</returns>
    private static List<List<string>> GroupPhysicalKeysBySlot(IList<string> keys)
    {
        var groups = new Dictionary<int, List<string>>();
        foreach (var key in keys)
        {
            var slot = CacheClusterTopology.GetHashSlot(key);
            if (!groups.TryGetValue(slot, out var group))
                groups.Add(slot, group = new List<string>());
            group.Add(key);
        }
        return groups.Values.ToList();
    }

    /// <summary>
    /// 按当前拓扑批量读取物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理键列表。</param>
    /// <returns>按物理键索引的缓存值；Provider 不支持批量读取时返回 <see langword="null"/>。</returns>
    private IDictionary<string, CacheValue<T>> GetAllPhysicalValues<T>(IList<string> keys)
    {
        if (_localProvider != null)
            return ReadHybridBatch<T>(keys);
        if (_cachingProvider.Database is IDatabase)
            return ReadRawRedisBatch<T>(keys);
        if (!IsCluster)
            return _cachingProvider.GetAll<T>(keys);
        var result = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        foreach (var group in GroupPhysicalKeysBySlot(keys))
        {
            var values = _cachingProvider.GetAll<T>(group);
            if (values == null)
                return null;
            foreach (var value in values)
                result[value.Key] = value.Value;
        }
        return result;
    }

    /// <summary>
    /// 异步按当前拓扑批量读取物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="keys">物理键列表。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按物理键索引的缓存值；Provider 不支持批量读取时返回 <see langword="null"/>。</returns>
    private async Task<IDictionary<string, CacheValue<T>>> GetAllPhysicalValuesAsync<T>(IList<string> keys,
        CancellationToken cancellationToken)
    {
        if (_localProvider != null)
            return await ReadHybridBatchAsync<T>(keys, cancellationToken);
        if (_cachingProvider.Database is IDatabase)
            return await ReadRawRedisBatchAsync<T>(keys, cancellationToken);
        if (!IsCluster)
            return await _cachingProvider.GetAllAsync<T>(keys, cancellationToken);
        var result = new Dictionary<string, CacheValue<T>>(keys.Count, StringComparer.Ordinal);
        var groups = GroupPhysicalKeysBySlot(keys);
        for (var offset = 0; offset < groups.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, groups.Count - offset);
            var tasks = new Task<IDictionary<string, CacheValue<T>>>[count];
            var dispatched = 0;
            IDictionary<string, CacheValue<T>>[] batches;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks[dispatched] = _cachingProvider.GetAllAsync<T>(groups[offset + dispatched], cancellationToken);
                }
                batches = await Task.WhenAll(tasks);
            }
            catch
            {
                await ObserveDispatchedAsync(tasks, dispatched);
                throw;
            }
            foreach (var batch in batches)
            {
                if (batch == null)
                    return null;
                foreach (var value in batch)
                    result[value.Key] = value.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// 按当前拓扑批量写入物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="values">物理键和值的映射。</param>
    /// <param name="expiration">缓存有效期。</param>
    private void SetPhysicalBatch<T>(IDictionary<string, T> values, TimeSpan expiration)
    {
        if (_coordinator != null)
        {
            _coordinator.WriteBatch(values, expiration);
            if (_localProvider != null)
                ReadHybridBatch<T>(values.Keys.ToArray());
            return;
        }
        if (!IsCluster)
        {
            ExecuteWithBusCheck(() => _provider.SetAll(values, expiration));
            return;
        }
        foreach (var group in GroupPhysicalValuesBySlot(values))
            ExecuteWithBusCheck(() => _provider.SetAll(group, expiration));
    }

    /// <summary>
    /// 异步按当前拓扑批量写入物理缓存键。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="values">物理键和值的映射。</param>
    /// <param name="expiration">缓存有效期。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task SetPhysicalBatchAsync<T>(IDictionary<string, T> values, TimeSpan expiration,
        CancellationToken cancellationToken)
    {
        if (_coordinator != null)
        {
            await _coordinator.WriteBatchAsync(values, expiration, cancellationToken).ConfigureAwait(false);
            if (_localProvider != null)
                await ReadHybridBatchAsync<T>(values.Keys.ToArray(), cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!IsCluster)
        {
            await ExecuteWithBusCheckAsync(() => _provider.SetAllAsync(values, expiration, cancellationToken));
            return;
        }
        var groups = GroupPhysicalValuesBySlot(values);
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
                    var group = groups[offset + dispatched];
                    tasks[dispatched] = ExecuteWithBusCheckAsync(() =>
                        _provider.SetAllAsync(group, expiration, cancellationToken));
                }
                await Task.WhenAll(tasks);
            }
            catch
            {
                await ObserveDispatchedAsync(tasks, dispatched);
                throw;
            }
        }
    }

    /// <summary>
    /// 按键的 Redis 哈希槽对批量写入值分组。
    /// </summary>
    /// <typeparam name="T">缓存值类型。</typeparam>
    /// <param name="values">物理键和值的映射。</param>
    /// <returns>每个分组内的键均属于同一 Redis 槽。</returns>
    private static List<Dictionary<string, T>> GroupPhysicalValuesBySlot<T>(IDictionary<string, T> values)
    {
        var groups = new Dictionary<int, Dictionary<string, T>>();
        foreach (var value in values)
        {
            var slot = CacheClusterTopology.GetHashSlot(value.Key);
            if (!groups.TryGetValue(slot, out var group))
                groups.Add(slot, group = new Dictionary<string, T>());
            group.Add(value.Key, value.Value);
        }
        return groups.Values.ToList();
    }

    /// <summary>
    /// 按拓扑拆分并删除物理缓存键。
    /// </summary>
    /// <param name="keys">待删除的物理键。</param>
    private void RemovePhysicalBatch(IList<string> keys)
    {
        if (_coordinator != null)
        {
            _coordinator.RemoveBatch(keys);
            return;
        }
        if (!IsCluster)
        {
            foreach (var batch in CacheDeleteBatchBudget.Partition(keys as IReadOnlyList<string> ?? keys.ToArray(), _configuration.BatchSize,
                         _configuration.Resources.MaxBatchCommandBytes))
                ExecuteWithBusCheck(() => _provider.RemoveAll(batch));
            return;
        }
        foreach (var group in GroupPhysicalKeysBySlot(keys))
        {
            foreach (var batch in CacheDeleteBatchBudget.Partition(group, _configuration.BatchSize,
                         _configuration.Resources.MaxBatchCommandBytes))
                ExecuteWithBusCheck(() => _provider.RemoveAll(batch));
        }
    }

    /// <summary>
    /// 异步按拓扑拆分并删除物理缓存键。
    /// </summary>
    /// <param name="keys">待删除的物理键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RemovePhysicalBatchAsync(IList<string> keys, CancellationToken cancellationToken)
    {
        if (_coordinator != null)
        {
            await _coordinator.RemoveBatchAsync(keys, cancellationToken).ConfigureAwait(false);
            return;
        }
        if (!IsCluster)
        {
            foreach (var batch in CacheDeleteBatchBudget.Partition(keys as IReadOnlyList<string> ?? keys.ToArray(), _configuration.BatchSize,
                         _configuration.Resources.MaxBatchCommandBytes))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteWithBusCheckAsync(() => _provider.RemoveAllAsync(batch, cancellationToken));
            }
            return;
        }
        var groups = GroupPhysicalKeysBySlot(keys);
        var batches = new List<IReadOnlyList<string>>();
        foreach (var group in groups)
            batches.AddRange(CacheDeleteBatchBudget.Partition(group, _configuration.BatchSize,
                _configuration.Resources.MaxBatchCommandBytes));
        for (var offset = 0; offset < batches.Count; offset += _configuration.MaxConcurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(_configuration.MaxConcurrency, batches.Count - offset);
            var tasks = new Task[count];
            var dispatched = 0;
            try
            {
                for (; dispatched < count; dispatched++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = batches[offset + dispatched];
                    tasks[dispatched] = ExecuteWithBusCheckAsync(() =>
                        _provider.RemoveAllAsync(batch, cancellationToken));
                }
                await Task.WhenAll(tasks);
            }
            catch
            {
                await ObserveDispatchedAsync(tasks, dispatched);
                throw;
            }
        }
    }

    /// <summary>
    /// 观察已派发的异步操作并忽略其后续异常。
    /// </summary>
    /// <param name="tasks">已派发的任务。</param>
    /// <param name="count">需要观察的任务数。</param>
    private static async Task ObserveDispatchedAsync(Task[] tasks, int count)
    {
        for (var index = 0; index < count; index++)
        {
            try { await tasks[index]; }
            catch { /* 保留派发或取消时的原始异常。 */ }
        }
    }

    /// <summary>
    /// 执行缓存操作并检查 Hybrid 失效总线状态。
    /// </summary>
    /// <param name="action">待执行的缓存操作。</param>
    private void ExecuteWithBusCheck(Action action)
    {
        using var operation = _busFailureTracker?.BeginOperation();
        action();
        operation?.ThrowIfFailed();
    }

    /// <summary>
    /// 执行缓存操作并检查 Hybrid 失效总线状态。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="action">待执行的缓存操作。</param>
    /// <returns>缓存操作的结果。</returns>
    private TResult ExecuteWithBusCheck<TResult>(Func<TResult> action)
    {
        using var operation = _busFailureTracker?.BeginOperation();
        var result = action();
        operation?.ThrowIfFailed();
        return result;
    }

    /// <summary>
    /// 异步执行缓存操作并检查 Hybrid 失效总线状态。
    /// </summary>
    /// <param name="action">待执行的异步缓存操作。</param>
    private async Task ExecuteWithBusCheckAsync(Func<Task> action)
    {
        using var operation = _busFailureTracker?.BeginOperation();
        await action().ConfigureAwait(false);
        operation?.ThrowIfFailed();
    }

    /// <summary>
    /// 异步执行缓存操作并检查 Hybrid 失效总线状态。
    /// </summary>
    /// <typeparam name="TResult">操作结果类型。</typeparam>
    /// <param name="action">待执行的异步缓存操作。</param>
    /// <returns>缓存操作的异步结果。</returns>
    private async Task<TResult> ExecuteWithBusCheckAsync<TResult>(Func<Task<TResult>> action)
    {
        using var operation = _busFailureTracker?.BeginOperation();
        var result = await action().ConfigureAwait(false);
        operation?.ThrowIfFailed();
        return result;
    }

    /// <summary>
    /// 保存扫描期间使用的主节点清单和槽位归属快照。
    /// </summary>
    private sealed class ScanTopology
    {
        /// <summary>
        /// 初始化一个 <see cref="ScanTopology"/> 类型的实例。
        /// </summary>
        /// <param name="servers">需要扫描的主节点。</param>
        /// <param name="slotOwnership">Redis 槽位归属快照。</param>
        public ScanTopology(List<IServer> servers, string slotOwnership)
        {
            Servers = servers;
            SlotOwnership = slotOwnership;
        }

        /// <summary>
        /// 获取本次扫描的主节点列表。
        /// </summary>
        public List<IServer> Servers { get; }

        /// <summary>
        /// 获取本次扫描的槽位归属快照。
        /// </summary>
        /// <remarks>非 Redis Cluster 拓扑时返回 <see langword="null"/>。</remarks>
        public string SlotOwnership { get; }
    }

    /// <summary>
    /// 校验同步扫描所需的主节点清单和 Cluster 视图。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <returns>可用于扫描的主节点及槽位归属快照。</returns>
    private ScanTopology ValidateClusterScanTopology(IDatabase database)
    {
        if (!IsCluster)
            return new ScanTopology(database.Multiplexer.GetEndPoints()
                .Select(endpoint => database.Multiplexer.GetServer(endpoint))
                .Where(server => !server.IsReplica)
                .ToList(), null);
        var servers = database.Multiplexer.GetEndPoints().Select(endpoint => database.Multiplexer.GetServer(endpoint)).ToArray();
        var seed = servers.FirstOrDefault(server => !server.IsReplica && server.IsConnected) ??
            throw new InvalidOperationException("Redis Cluster 没有可用主节点。");
        var nodes = seed.Execute("CLUSTER", "NODES").ToString();
        var expected = CacheClusterTopology.GetMasterIds(nodes);
        var slotOwnership = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        var scanServers = new List<IServer>();
        foreach (var server in servers)
        {
            if (server.IsReplica || !server.IsConnected)
                continue;
            var view = server.EndPoint.Equals(seed.EndPoint) ? nodes :
                server.Execute("CLUSTER", "NODES").ToString();
            if (!server.EndPoint.Equals(seed.EndPoint))
                CacheClusterTopology.EnsureSameView(slotOwnership, view);
            var id = CacheClusterTopology.GetLocalMasterId(view);
            if (actual.Add(id))
                scanServers.Add(server);
        }
        if (!actual.SetEquals(expected))
            throw new InvalidOperationException("Redis Cluster 扫描节点清单不完整或已过期。");
        return new ScanTopology(scanServers, slotOwnership);
    }

    /// <summary>
    /// 异步校验扫描所需的主节点清单和 Cluster 视图。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>可用于扫描的主节点及槽位归属快照。</returns>
    private async Task<ScanTopology> ValidateClusterScanTopologyAsync(IDatabase database,
        CancellationToken cancellationToken)
    {
        if (!IsCluster)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ScanTopology(database.Multiplexer.GetEndPoints()
                .Select(endpoint => database.Multiplexer.GetServer(endpoint))
                .Where(server => !server.IsReplica)
                .ToList(), null);
        }
        cancellationToken.ThrowIfCancellationRequested();
        var servers = database.Multiplexer.GetEndPoints().Select(endpoint => database.Multiplexer.GetServer(endpoint)).ToArray();
        var seed = servers.FirstOrDefault(server => !server.IsReplica && server.IsConnected) ??
            throw new InvalidOperationException("Redis Cluster 没有可用主节点。");
        var nodes = (await seed.ExecuteAsync("CLUSTER", "NODES")).ToString();
        var expected = CacheClusterTopology.GetMasterIds(nodes);
        var slotOwnership = CacheClusterTopology.GetSlotOwnershipSnapshot(nodes);
        var actual = new HashSet<string>(StringComparer.Ordinal);
        var scanServers = new List<IServer>();
        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (server.IsReplica || !server.IsConnected)
                continue;
            var view = server.EndPoint.Equals(seed.EndPoint) ? nodes :
                (await server.ExecuteAsync("CLUSTER", "NODES")).ToString();
            if (!server.EndPoint.Equals(seed.EndPoint))
                CacheClusterTopology.EnsureSameView(slotOwnership, view);
            var id = CacheClusterTopology.GetLocalMasterId(view);
            if (actual.Add(id))
                scanServers.Add(server);
        }
        if (!actual.SetEquals(expected))
            throw new InvalidOperationException("Redis Cluster 扫描节点清单不完整或已过期。");
        return new ScanTopology(scanServers, slotOwnership);
    }

    /// <summary>
    /// 验证扫描结束时 Cluster 槽位归属未发生变化。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="expectedSlotOwnership">扫描开始时的槽位归属快照。</param>
    private void EnsureClusterScanTopologyUnchanged(IDatabase database, string expectedSlotOwnership)
    {
        if (!IsCluster)
            return;
        CacheClusterTopology.EnsureUnchanged(expectedSlotOwnership,
            ValidateClusterScanTopology(database).SlotOwnership);
    }

    /// <summary>
    /// 异步验证扫描结束时 Cluster 槽位归属未发生变化。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="expectedSlotOwnership">扫描开始时的槽位归属快照。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task EnsureClusterScanTopologyUnchangedAsync(IDatabase database,
        string expectedSlotOwnership, CancellationToken cancellationToken)
    {
        if (!IsCluster)
            return;
        cancellationToken.ThrowIfCancellationRequested();
        CacheClusterTopology.EnsureUnchanged(expectedSlotOwnership,
            (await ValidateClusterScanTopologyAsync(database, cancellationToken)).SlotOwnership);
    }
}
