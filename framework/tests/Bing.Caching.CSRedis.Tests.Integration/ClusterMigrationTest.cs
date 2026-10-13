using System.Text;
using System.Runtime.ExceptionServices;
using Bing.Caching.CSRedis;
using CSRedis;
using Xunit;

namespace Bing.Caching.CSRedis.Tests;

/// <summary>
/// 控制 Redis Cluster 槽位迁移测试是否执行。
/// </summary>
public sealed class ClusterMigrationFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterMigrationFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterMigrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_MIGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需要独占的可丢弃三主三从 Redis Cluster 迁移测试环境。";
    }
}

/// <summary>
/// 验证 Redis Cluster 槽位迁移和重定向期间的缓存行为。
/// </summary>
public sealed class ClusterMigrationTest
{
    /// <summary>
    /// 保存 Redis Cluster 主节点标识、端点和槽位范围。
    /// </summary>
    /// <param name="Id">对象标识。</param>
    /// <param name="Endpoint">Redis 端点。</param>
    /// <param name="Slots">Redis 槽位集合。</param>
    private sealed record Master(string Id, string Endpoint, IReadOnlyList<(int Start, int End)> Slots);

    /// <summary>
    /// 验证前缀流式扫描期间槽位所有者变化会抛出错误，而不是完成读取。
    /// </summary>
    [ClusterMigrationFact]
    public async Task SlotOwnerChangesDuringScan_RejectsCompletedStream()
    {
        var masters = await ReadMastersAsync();
        Assert.Equal(3, masters.Count);
        var configuration = new CacheConfiguration("cs-scan-move-" + Guid.NewGuid().ToString("N"),
            "redis", topology: CacheTopology.RedisCluster, batchSize: 2);
        using var router = new CSRedisClient(masters[0].Endpoint);
        var cache = new CSRedisCacheManager(router, configuration, new[] { router });
        for (var index = 0; index < 120; index++)
            await cache.SetAsync("scan:item:" + index, index + 1);

        var source = masters[0];
        var destination = masters[1];
        int? emptySlot = null;
        foreach (var range in source.Slots)
        {
            for (var slot = range.Start; slot <= range.End; slot++)
            {
                var count = await CallNodeAsync(source, "CLUSTER", new[]
                {
                    "COUNTKEYSINSLOT", slot.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
                if (Convert.ToInt64(count, System.Globalization.CultureInfo.InvariantCulture) != 0)
                    continue;
                emptySlot = slot;
                break;
            }
            if (emptySlot.HasValue) break;
        }
        Assert.True(emptySlot.HasValue, "需要一个不含数据的槽位供测试迁移。");
        var slotText = emptySlot.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var moved = false;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();
        try
        {
            await using var stream = cache.GetByPrefixStreamAsync<int>("scan:item:").GetAsyncEnumerator();
            Assert.True(await stream.MoveNextAsync());
            Assert.True(stream.Current.Hit);

            moved = true;
            foreach (var master in masters)
                await CallNodeAsync(master, "CLUSTER", new[] { "SETSLOT", slotText, "NODE", destination.Id });
            Assert.Equal(masters.Select(master => master.Id).OrderBy(id => id),
                (await ReadMastersAsync()).Select(master => master.Id).OrderBy(id => id));

            var error = await Record.ExceptionAsync(async () =>
            {
                while (await stream.MoveNextAsync()) { }
            });
            Assert.IsType<InvalidOperationException>(error);
            Assert.Contains("槽位归属发生变化", error.Message);
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            if (moved)
            {
                foreach (var master in masters)
                {
                    try
                    {
                        await CallNodeAsync(master, "CLUSTER", new[] { "SETSLOT", slotText, "NODE", source.Id });
                    }
                    catch (Exception exception)
                    {
                        cleanupErrors.Add(exception);
                    }
                }
            }
            try { await cache.ClearAsync(); }
            catch (Exception exception) { cleanupErrors.Add(exception); }
        }
        if (testError != null)
        {
            if (cleanupErrors.Count > 0)
                testError.Data["RedisClusterScanMoveCleanupError"] =
                    new AggregateException(cleanupErrors).ToString();
            ExceptionDispatchInfo.Capture(testError).Throw();
        }
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Redis Cluster 扫描迁移测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 验证 MOVED 重定向期间批量和前缀读取的错误传播。
    /// </summary>
    /// <remarks>
    /// 读取成功时返回目标值；失败时传播重定向错误，不静默判定为未命中。
    /// </remarks>
    [ClusterMigrationFact]
    public async Task MovedRedirect_DoesNotReportNewOwnerValueAsCacheMiss()
    {
        var masters = await ReadMastersAsync();
        Assert.Equal(3, masters.Count);
        var configuration = new CacheConfiguration("cs-moved-" + Guid.NewGuid().ToString("N"), "redis",
            topology: CacheTopology.RedisCluster, batchSize: 2);
        const string logicalKey = "moved:item";
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);
        var slot = CacheClusterTopology.GetHashSlot(physicalKey);
        var source = masters.Single(master => master.Slots.Any(range => range.Start <= slot && slot <= range.End));
        var destination = masters.First(master => master.Id != source.Id);
        var slotText = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, Convert.ToInt64(await CallNodeAsync(source, "CLUSTER",
            new[] { "COUNTKEYSINSLOT", slotText }), System.Globalization.CultureInfo.InvariantCulture));

        using var router = new CSRedisClient(source.Endpoint);
        var cache = new CSRedisCacheManager(router, configuration, new[] { router });
        Exception testError = null;
        var cleanupErrors = new List<Exception>();
        try
        {
            foreach (var master in masters)
                await CallNodeAsync(master, "CLUSTER", new[] { "SETSLOT", slotText, "NODE", destination.Id });

            await CallNodeAsync(destination, "SET", new[] { physicalKey, "42" });
            var moved = await Record.ExceptionAsync(() =>
                CallNodeAsync(source, "GET", new[] { physicalKey }));
            Assert.NotNull(moved);
            Assert.Contains("MOVED", moved.ToString(), StringComparison.OrdinalIgnoreCase);

            var readError = await Record.ExceptionAsync(async () =>
                Assert.Equal(42, Assert.Single(await cache.GetAllAsync<int>(new[] { logicalKey }))));
            if (readError != null)
                Assert.Contains("MOVED", readError.ToString(), StringComparison.OrdinalIgnoreCase);

            var scanError = await Record.ExceptionAsync(async () =>
                Assert.Equal(42, Assert.Single(await cache.GetByPrefixAsync<int>("moved:"))));
            if (scanError != null)
                Assert.Contains("MOVED", scanError.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            try
            {
                await CallNodeAsync(destination, "DEL", new[] { physicalKey });
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
            foreach (var master in masters)
            {
                try
                {
                    await CallNodeAsync(master, "CLUSTER", new[] { "SETSLOT", slotText, "NODE", source.Id });
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
        }
        if (testError != null)
        {
            if (cleanupErrors.Count > 0)
                testError.Data["RedisClusterMovedCleanupError"] =
                    new AggregateException(cleanupErrors).ToString();
            ExceptionDispatchInfo.Capture(testError).Throw();
        }
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Redis Cluster MOVED 测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 验证 ASK 迁移期间批量和前缀读取的重定向处理。
    /// </summary>
    /// <remarks>
    /// 读取成功时跟随重定向；失败时传播 ASK 错误，不静默丢失缓存值。
    /// </remarks>
    [ClusterMigrationFact]
    public async Task AskRedirect_DoesNotReportMigratedKeyAsCacheMiss()
    {
        using var discovery = new CSRedisClient("127.0.0.1:7000");
        var nodes = discovery.Nodes.Values.First();
        using var discoveryConnection = await nodes.GetAsync();
        var response = await discoveryConnection.Value.CallAsync("CLUSTER", new[] { "NODES" });
        var topology = response is byte[] bytes ? Encoding.UTF8.GetString(bytes) : response.ToString();
        var masters = ParseMasters(topology);
        Assert.Equal(3, masters.Count);

        var configuration = new CacheConfiguration("cs-ask-" + Guid.NewGuid().ToString("N"), "redis",
            topology: CacheTopology.RedisCluster, batchSize: 2);
        const string logicalKey = "migration:item";
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);
        var slot = CacheClusterTopology.GetHashSlot(physicalKey);
        var source = masters.Single(master => master.Slots.Any(range => range.Start <= slot && slot <= range.End));
        var destination = masters.First(master => master.Id != source.Id);
        using var sourceClient = new CSRedisClient(source.Endpoint);
        using var destinationClient = new CSRedisClient(destination.Endpoint);
        var sourceNode = sourceClient.Nodes.Single(entry =>
            entry.Key.Contains(source.Endpoint, StringComparison.OrdinalIgnoreCase)).Value;
        var destinationNode = destinationClient.Nodes.Single(entry =>
            entry.Key.Contains(destination.Endpoint, StringComparison.OrdinalIgnoreCase)).Value;
        using var sourceConnection = await sourceNode.GetAsync();
        using var destinationConnection = await destinationNode.GetAsync();
        using var router = new CSRedisClient(source.Endpoint);
        var cache = new CSRedisCacheManager(router, configuration, new[] { router });
        var slotText = slot.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var target = destination.Endpoint.Split(':');
        Exception testError = null;
        var cleanupErrors = new List<Exception>();
        var sourceMigrating = false;
        var destinationImporting = false;

        try
        {
            await cache.SetAsync(logicalKey, 42);
            await sourceConnection.Value.CallAsync("CLUSTER", new[] { "SETSLOT", slotText, "MIGRATING", destination.Id });
            sourceMigrating = true;
            await destinationConnection.Value.CallAsync("CLUSTER", new[] { "SETSLOT", slotText, "IMPORTING", source.Id });
            destinationImporting = true;
            await sourceConnection.Value.CallAsync("MIGRATE", new[] { target[0], target[1], physicalKey, "0", "5000" });

            var ask = await Record.ExceptionAsync(async () =>
                await sourceConnection.Value.CallAsync("GET", new[] { physicalKey }));
            Assert.NotNull(ask);
            Assert.Contains("ASK", ask.ToString(), StringComparison.OrdinalIgnoreCase);

            // 客户端可以跟随 ASK，也可以显式失败；不能成功返回丢失的缓存值。
            var readError = await Record.ExceptionAsync(async () =>
                Assert.Equal(42, Assert.Single(await cache.GetAllAsync<int>(new[] { logicalKey }))));
            if (readError != null)
                Assert.Contains("ASK", readError.ToString(), StringComparison.OrdinalIgnoreCase);

            var scanError = await Record.ExceptionAsync(async () =>
                Assert.Equal(42, Assert.Single(await cache.GetByPrefixAsync<int>("migration:"))));
            if (scanError != null)
                Assert.Contains("ASK", scanError.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            if (destinationImporting)
            {
                try
                {
                    await destinationConnection.Value.CallAsync("ASKING", Array.Empty<string>());
                    await destinationConnection.Value.CallAsync("DEL", new[] { physicalKey });
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            if (sourceMigrating)
            {
                try
                {
                    await sourceConnection.Value.CallAsync("CLUSTER", new[] { "SETSLOT", slotText, "STABLE" });
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            if (destinationImporting)
            {
                try
                {
                    await destinationConnection.Value.CallAsync("CLUSTER", new[] { "SETSLOT", slotText, "STABLE" });
                }
                catch (Exception exception)
                {
                    cleanupErrors.Add(exception);
                }
            }
            try
            {
                await sourceConnection.Value.CallAsync("DEL", new[] { physicalKey });
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
        }
        if (testError != null)
        {
            if (cleanupErrors.Count > 0)
                testError.Data["RedisClusterMigrationCleanupError"] =
                    new AggregateException(cleanupErrors).ToString();
            ExceptionDispatchInfo.Capture(testError).Throw();
        }
        if (cleanupErrors.Count > 0)
            throw new AggregateException("Redis Cluster 迁移测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 解析 Redis Cluster 节点清单中的主节点和槽位范围。
    /// </summary>
    /// <param name="topology">Redis Cluster 节点清单。</param>
    /// <returns>返回解析后的主节点列表。</returns>
    private static List<Master> ParseMasters(string topology)
    {
        var masterIds = CacheClusterTopology.GetMasterIds(topology);
        var result = new List<Master>();
        foreach (var line in topology.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!masterIds.Contains(fields[0]))
                continue;
            var ranges = new List<(int Start, int End)>();
            foreach (var token in fields.Skip(8))
            {
                if (token.StartsWith("[", StringComparison.Ordinal))
                    continue;
                var bounds = token.Split('-');
                var start = int.Parse(bounds[0], System.Globalization.CultureInfo.InvariantCulture);
                var end = bounds.Length == 1 ? start :
                    int.Parse(bounds[1], System.Globalization.CultureInfo.InvariantCulture);
                ranges.Add((start, end));
            }
            result.Add(new Master(fields[0], fields[1].Split('@')[0], ranges));
        }
        return result;
    }

    /// <summary>
    /// 读取 Redis Cluster 当前主节点列表。
    /// </summary>
    /// <returns>包含当前主节点列表的异步任务。</returns>
    private static async Task<List<Master>> ReadMastersAsync()
    {
        using var discovery = new CSRedisClient("127.0.0.1:7000");
        var node = discovery.Nodes.Values.First();
        using var connection = await node.GetAsync();
        var response = await connection.Value.CallAsync("CLUSTER", new[] { "NODES" });
        var topology = response is byte[] bytes ? Encoding.UTF8.GetString(bytes) : response.ToString();
        return ParseMasters(topology);
    }

    /// <summary>
    /// 向指定 Redis Cluster 主节点发送命令并返回响应。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis 命令响应。</returns>
    /// <param name="master">目标 Redis 主节点。</param>
    /// <param name="command">Redis 命令。</param>
    /// <param name="arguments">命令行参数。</param>
    private static async Task<object> CallNodeAsync(Master master, string command, string[] arguments)
    {
        using var client = new CSRedisClient(master.Endpoint);
        var node = client.Nodes.Single(entry =>
            entry.Key.Contains(master.Endpoint, StringComparison.OrdinalIgnoreCase)).Value;
        using var connection = await node.GetAsync();
        return await connection.Value.CallAsync(command, arguments);
    }
}
