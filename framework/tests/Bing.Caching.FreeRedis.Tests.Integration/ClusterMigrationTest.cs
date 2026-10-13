using System.Globalization;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;
using Bing.Caching.FreeRedis;
using Bing.Test.Shared;
using FreeRedis;
using Newtonsoft.Json;

namespace Bing.Caching.FreeRedis.Tests;

/// <summary>
/// Redis Cluster 槽位迁移期测试门控。
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
            Skip = "需在可丢弃的三主 Redis Cluster 上同时启用 RUN_INTEGRATION_TESTS=true 和 RUN_CACHE_CLUSTER_MIGRATION_TESTS=true。";
    }
}

/// <summary>
/// Redis Cluster 槽位所有权切换测试门控。
/// </summary>
public sealed class ClusterMovedFactAttribute : Xunit.FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="ClusterMovedFactAttribute"/> 类型的实例。
    /// </summary>
    public ClusterMovedFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CLUSTER_MIGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase))
            Skip = "需在可丢弃的三主 Redis Cluster 上同时启用 RUN_INTEGRATION_TESTS=true 和 RUN_CACHE_CLUSTER_MIGRATION_TESTS=true。";
    }
}

/// <summary>
/// FreeRedis Redis Cluster 槽位迁移测试。
/// </summary>
/// <remarks>
/// 覆盖 MIGRATING/IMPORTING 阶段，确认不静默丢失数据或吞掉重定向错误。
/// </remarks>
public sealed class ClusterMigrationTest
{
    /// <summary>
    /// 保存 Redis Cluster 种子端点集合。
    /// </summary>
    private static readonly string[] SeedEndpoints =
    {
        "127.0.0.1:7000",
        "127.0.0.1:7001",
        "127.0.0.1:7002"
    };

    /// <summary>
    /// 保存 Redis Cluster 槽位总数。
    /// </summary>
    private const int ClusterSlotCount = 16384;

    /// <summary>
    /// 槽位迁移期间批量读取和扫描保留已有值或传播重定向错误。
    /// </summary>
    [ClusterMigrationFact]
    public async Task MigratingSlot_BatchReadAndScanPreserveExistingValuesOrPropagateRedirectFailure()
    {
        var clients = SeedEndpoints.Select(CreateClient).ToArray();
        using var router = CreateClusterRouter();
        var physicalKeys = new List<string>();
        Exception testError = null;
        Exception cleanupError = null;
        var migrationSlot = -1;
        RedisClient sourceClient = null;
        RedisClient targetClient = null;
        string migratedPhysicalKey = null;
        string targetAdvertisedEndpoint = null;

        try
        {
            var nodeIds = await ReadNodeIdsAsync(clients);
            var topology = await ReadTopologyAsync(clients[0]);
            Assert.Equal(SeedEndpoints.Length, topology.Masters.Count);
            Assert.All(topology.Masters, master => Assert.True(nodeIds.ContainsKey(master.Id),
                "CLUSTER NODES 中的主节点必须能由固定的 127.0.0.1:7000-7002 种子访问。"));

            var configuration = new CacheConfiguration(
                "freeredis-cluster-migration-" + Guid.NewGuid().ToString("N"),
                "redis",
                batchSize: 500,
                maxConcurrency: 4,
                topology: CacheTopology.RedisCluster);
            var prefix = "migration-item:" + Guid.NewGuid().ToString("N") + ":";
            var selected = SelectSameSlotKeys(configuration, prefix, topology);
            migrationSlot = selected.Slot;

            var source = topology.Masters.Single(master => master.OwnsSlot(migrationSlot));
            var target = topology.Masters.First(master => master.Id != source.Id);
            sourceClient = clients[nodeIds[source.Id]];
            targetClient = clients[nodeIds[target.Id]];
            targetAdvertisedEndpoint = target.Endpoint;
            var cache = new FreeRedisCacheManager(router, configuration, clients);

            var existingItems = selected.Items.Take(3).ToArray();
            var missingItem = selected.Items[3];
            physicalKeys.AddRange(existingItems.Select(item => item.PhysicalKey));
            var expected = existingItems.Select((item, index) => 700 + index).ToArray();
            await cache.SetAllAsync(existingItems.Select((item, index) => new
            {
                item.LogicalKey,
                Value = expected[index]
            }).ToDictionary(
                item => item.LogicalKey,
                item => item.Value));

            Assert.Equal(expected[0], await cache.GetAsync<int>(existingItems[0].LogicalKey));
            var directExisting = await sourceClient.CallAsync(
                new CommandPacket("GET").Input(new object[] { existingItems[0].PhysicalKey }));
            Assert.NotNull(directExisting);

            await sourceClient.CallAsync(CreateSetSlotPacket(migrationSlot, "MIGRATING", target.Id));
            await targetClient.CallAsync(CreateSetSlotPacket(migrationSlot, "IMPORTING", source.Id));

            migratedPhysicalKey = existingItems[0].PhysicalKey;
            var targetEndpoint = ParseEndpoint(targetAdvertisedEndpoint);
            await sourceClient.CallAsync(new CommandPacket("MIGRATE").Input(new object[]
            {
                targetEndpoint.Host,
                targetEndpoint.Port,
                string.Empty,
                0,
                5000,
                "KEYS",
                migratedPhysicalKey
            }));

            var movedExistingError = await Record.ExceptionAsync(() => sourceClient.CallAsync(
                new CommandPacket("GET").Input(new object[] { migratedPhysicalKey })));
            Assert.NotNull(movedExistingError);
            AssertRedirectFailure(movedExistingError, "迁移源节点对已迁移键的 GET");

            var missingError = await Record.ExceptionAsync(() => sourceClient.CallAsync(
                new CommandPacket("GET").Input(new object[] { missingItem.PhysicalKey })));
            Assert.NotNull(missingError);
            AssertRedirectFailure(missingError, "迁移源节点对缺失键的 GET");

            List<int?> batchValues = null;
            var batchError = await Record.ExceptionAsync(async () =>
            {
                batchValues = await cache.GetAllAsync<int?>(
                    existingItems.Select(item => item.LogicalKey).Append(missingItem.LogicalKey));
            });
            if (batchError == null)
            {
                Assert.NotNull(batchValues);
                Assert.Equal(expected.Select(value => (int?)value), batchValues.Take(3));
                Assert.Null(batchValues[3]);
            }
            else
            {
                AssertRedirectFailure(batchError, "槽位迁移期批量读取");
            }

            List<int> scannedValues = null;
            var scanError = await Record.ExceptionAsync(async () =>
            {
                scannedValues = await cache.GetByPrefixAsync<int>(prefix);
            });
            if (scanError == null)
            {
                Assert.NotNull(scannedValues);
                Assert.Equal(expected.OrderBy(value => value), scannedValues.OrderBy(value => value));
            }
            else
            {
                AssertRedirectFailure(scanError, "槽位迁移期前缀扫描");
            }
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            try
            {
                cleanupError = await RestoreSlotAndRemoveKeysAsync(
                    clients, router, migrationSlot, physicalKeys, migratedPhysicalKey, sourceClient,
                    targetAdvertisedEndpoint);
            }
            catch (Exception exception)
            {
                cleanupError = exception;
            }
            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupError ??= exception;
                }
            }
        }

        if (testError != null && cleanupError != null)
            testError.Data["RedisClusterMigrationCleanupError"] = cleanupError.ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupError != null)
            throw cleanupError;
    }

    /// <summary>
    /// 槽位所有权迁移后批量读取和扫描保留值或传播 MOVED 错误。
    /// </summary>
    [ClusterMovedFact]
    public async Task MovedSlot_BatchReadAndScanPreserveValueOrPropagateRedirectFailure()
    {
        var clients = SeedEndpoints.Select(CreateClient).ToArray();
        using var router = CreateClusterRouter();
        Exception testError = null;
        Exception cleanupError = null;
        var slot = -1;
        var originalSourceId = null as string;
        var physicalKey = null as string;
        RedisClient sourceClient = null;
        RedisClient targetClient = null;

        try
        {
            var nodeIds = await ReadNodeIdsAsync(clients);
            var topology = await ReadTopologyAsync(clients[0]);
            Assert.Equal(SeedEndpoints.Length, topology.Masters.Count);
            Assert.All(topology.Masters, master => Assert.True(nodeIds.ContainsKey(master.Id),
                "CLUSTER NODES 中的主节点必须能由固定的 127.0.0.1:7000-7002 种子访问。"));

            var configuration = new CacheConfiguration(
                "freeredis-cluster-moved-" + Guid.NewGuid().ToString("N"),
                "redis",
                batchSize: 500,
                maxConcurrency: 4,
                topology: CacheTopology.RedisCluster);
            var prefix = "moved-item:" + Guid.NewGuid().ToString("N") + ":";
            SelectedKey selectedKey = null;
            ClusterMaster source = null;
            var examinedSlots = new HashSet<int>();
            for (var index = 0; index < 10_000 && selectedKey == null; index++)
            {
                var logicalKey = prefix + index.ToString(CultureInfo.InvariantCulture);
                var candidatePhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);
                var candidateSlot = CacheClusterTopology.GetHashSlot(candidatePhysicalKey);
                if (!examinedSlots.Add(candidateSlot))
                    continue;
                var candidateSource = topology.Masters.Single(master => master.OwnsSlot(candidateSlot));
                var candidateSourceClient = clients[nodeIds[candidateSource.Id]];
                if (await CountKeysInSlotAsync(candidateSourceClient, candidateSlot) != 0)
                    continue;
                selectedKey = new SelectedKey(logicalKey, candidatePhysicalKey);
                source = candidateSource;
                slot = candidateSlot;
                sourceClient = candidateSourceClient;
            }

            Assert.NotNull(selectedKey);
            Assert.NotNull(source);
            originalSourceId = source.Id;
            var target = topology.Masters.First(master => master.Id != source.Id);
            targetClient = clients[nodeIds[target.Id]];
            var cache = new FreeRedisCacheManager(router, configuration, clients);

            await SetSlotNodeOnAllMastersAsync(clients, slot, target.Id);
            await targetClient.SetAsync(selectedKey.PhysicalKey, 909, 300);
            physicalKey = selectedKey.PhysicalKey;

            var movedError = await Record.ExceptionAsync(() => sourceClient.CallAsync(
                new CommandPacket("GET").Input(new object[] { physicalKey })));
            Assert.NotNull(movedError);
            AssertRedirectFailure(movedError, "槽位切换后旧源节点的 GET");

            List<int?> batchValues = null;
            var batchError = await Record.ExceptionAsync(async () =>
            {
                batchValues = await cache.GetAllAsync<int?>(new[] { selectedKey.LogicalKey });
            });
            if (batchError == null)
            {
                Assert.NotNull(batchValues);
                Assert.Single(batchValues);
                Assert.Equal(909, batchValues[0]);
            }
            else
            {
                AssertRedirectFailure(batchError, "槽位切换后的批量读取");
            }

            List<int> scannedValues = null;
            var scanError = await Record.ExceptionAsync(async () =>
            {
                scannedValues = await cache.GetByPrefixAsync<int>(prefix);
            });
            if (scanError == null)
            {
                Assert.NotNull(scannedValues);
                Assert.Equal(new[] { 909 }, scannedValues);
            }
            else
            {
                AssertRedirectFailure(scanError, "槽位切换后的前缀扫描");
            }
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            try
            {
                cleanupError = await RestoreMovedSlotAsync(
                    clients, targetClient, slot, originalSourceId, physicalKey);
            }
            catch (Exception exception)
            {
                cleanupError = exception;
            }
            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupError ??= exception;
                }
            }
        }

        if (testError != null && cleanupError != null)
            testError.Data["RedisClusterMovedCleanupError"] = cleanupError.ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupError != null)
            throw cleanupError;
    }

    /// <summary>
    /// 流式扫描期间槽位归属变化时，使用稳定主节点标识检测并拒绝不完整结果。
    /// </summary>
    [ClusterMovedFact]
    public async Task SlotOwnershipChangeDuringStreamingScan_IsRejectedWithStableMasterIds()
    {
        var clients = SeedEndpoints.Select(CreateClient).ToArray();
        using var router = CreateClusterRouter();
        var physicalKeys = new List<string>();
        Exception testError = null;
        Exception cleanupError = null;
        var movedSlot = -1;
        string originalOwnerId = null;

        try
        {
            var nodeIds = await ReadNodeIdsAsync(clients);
            var topology = await ReadTopologyAsync(clients[0]);
            Assert.Equal(SeedEndpoints.Length, topology.Masters.Count);
            Assert.All(topology.Masters, master => Assert.True(nodeIds.ContainsKey(master.Id),
                "CLUSTER NODES 中的主节点必须能由固定的 127.0.0.1:7000-7002 种子访问。"));

            var configuration = new CacheConfiguration(
                "freeredis-cluster-slot-scan-" + Guid.NewGuid().ToString("N"),
                "redis",
                batchSize: 8,
                maxConcurrency: 4,
                topology: CacheTopology.RedisCluster);
            var prefix = "slot-scan-item:" + Guid.NewGuid().ToString("N") + ":";
            var values = new Dictionary<string, int>(256, StringComparer.Ordinal);
            var testSlots = new HashSet<int>();
            for (var index = 0; index < 256; index++)
            {
                var logicalKey = prefix + index.ToString(CultureInfo.InvariantCulture);
                var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);
                values.Add(logicalKey, index);
                physicalKeys.Add(physicalKey);
                testSlots.Add(CacheClusterTopology.GetHashSlot(physicalKey));
            }

            var source = topology.Masters.FirstOrDefault(master =>
                master.Slots.Any(slot => !testSlots.Contains(slot)));
            Assert.NotNull(source);
            movedSlot = source.Slots.First(slot => !testSlots.Contains(slot));
            originalOwnerId = source.Id;
            var target = topology.Masters.First(master => !string.Equals(master.Id, source.Id,
                StringComparison.Ordinal));
            var cache = new FreeRedisCacheManager(router, configuration, clients);
            await cache.SetAllAsync(values);

            var enumerator = cache.GetByPrefixStreamAsync<int>(prefix).GetAsyncEnumerator();
            try
            {
                Assert.True(await enumerator.MoveNextAsync(), "测试数据必须至少产生一个流式扫描结果。");
                Assert.True(enumerator.Current.Hit);

                // 变更所有主节点上的同一槽位归属；主节点身份集合保持不变，只有槽位快照变化。
                await SetSlotNodeOnAllMastersAsync(clients, movedSlot, target.Id);
                var afterMove = await ReadTopologyAsync(clients[0]);
                Assert.Equal(
                    topology.Masters.Select(master => master.Id).OrderBy(id => id),
                    afterMove.Masters.Select(master => master.Id).OrderBy(id => id));
                Assert.Equal(target.Id, afterMove.Masters.Single(master => master.Slots.Contains(movedSlot)).Id);

                var scanError = await Record.ExceptionAsync(async () =>
                {
                    while (await enumerator.MoveNextAsync())
                    {
                    }
                });
                Assert.NotNull(scanError);
                var invalidOperation = Assert.IsType<InvalidOperationException>(scanError);
                Assert.Contains("槽位归属发生变化", invalidOperation.Message, StringComparison.Ordinal);
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            try
            {
                if (movedSlot >= 0 && !string.IsNullOrWhiteSpace(originalOwnerId))
                    await SetSlotNodeOnAllMastersAsync(clients, movedSlot, originalOwnerId);

                foreach (var physicalKey in physicalKeys)
                {
                    try
                    {
                        await router.DelAsync(new[] { physicalKey });
                    }
                    catch (Exception exception)
                    {
                        cleanupError ??= new InvalidOperationException(
                            "槽位归属扫描测试清理阶段精确删除测试键失败。键: " + physicalKey,
                            exception);
                    }
                }
            }
            catch (Exception exception)
            {
                cleanupError ??= exception;
            }

            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupError ??= exception;
                }
            }
        }

        if (testError != null && cleanupError != null)
            testError.Data["RedisClusterSlotScanCleanupError"] = cleanupError.ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupError != null)
            throw cleanupError;
    }

    /// <summary>
    /// 读取指定 Redis 节点负责的槽位数量。
    /// </summary>
    /// <returns>异步操作，完成后返回指定槽位中的键数量。</returns>
    /// <param name="client">Redis 客户端。</param>
    /// <param name="slot">Redis 槽位。</param>
    private static async Task<long> CountKeysInSlotAsync(RedisClient client, int slot)
    {
        var response = await client.CallAsync(new CommandPacket("CLUSTER", "COUNTKEYSINSLOT")
            .Input(new object[] { slot }));
        if (!long.TryParse(Convert.ToString(response, CultureInfo.InvariantCulture),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0)
            throw new InvalidOperationException("无法解析 Redis Cluster COUNTKEYSINSLOT 回复。");
        return count;
    }

    /// <summary>
    /// 将目标主节点设置为所有主节点视图中的槽位所有者。
    /// </summary>
    /// <param name="clients">Redis 节点客户端集合。</param>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="targetNodeId">目标节点标识。</param>
    private static async Task SetSlotNodeOnAllMastersAsync(IReadOnlyList<RedisClient> clients, int slot,
        string targetNodeId)
    {
        for (var nodeIndex = 0; nodeIndex < clients.Count; nodeIndex++)
        {
            await clients[nodeIndex].CallAsync(CreateSetSlotNodePacket(slot, targetNodeId));
        }
    }

    /// <summary>
    /// 创建将指定槽位标记为由目标节点负责的命令包。
    /// </summary>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="nodeId">目标 Redis 节点标识。</param>
    /// <returns>用于设置槽位节点归属的 Redis 命令包。</returns>
    private static CommandPacket CreateSetSlotNodePacket(int slot, string nodeId) =>
        new CommandPacket("CLUSTER", "SETSLOT").Input(new object[] { slot, "NODE", nodeId });

    /// <summary>
    /// 删除迁移测试键并将槽位归属恢复到源节点。
    /// </summary>
    /// <returns>异步操作，完成后返回恢复过程中捕获的异常；恢复成功时返回 null。</returns>
    /// <param name="clients">Redis 节点客户端集合。</param>
    /// <param name="targetClient">目标客户端。</param>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="sourceNodeId">源节点标识。</param>
    /// <param name="physicalKey">物理缓存键。</param>
    private static async Task<Exception> RestoreMovedSlotAsync(IReadOnlyList<RedisClient> clients,
        RedisClient targetClient, int slot, string sourceNodeId, string physicalKey)
    {
        var errors = new List<Exception>();
        if (targetClient != null && !string.IsNullOrWhiteSpace(physicalKey))
        {
            try
            {
                await targetClient.CallAsync(new CommandPacket("DEL")
                    .Input(new object[] { physicalKey }));
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    "MOVED 清理阶段 target key DEL 失败。键: " + physicalKey, exception));
            }
        }

        if (slot >= 0 && !string.IsNullOrWhiteSpace(sourceNodeId))
        {
            for (var nodeIndex = 0; nodeIndex < clients.Count; nodeIndex++)
            {
                try
                {
                    await clients[nodeIndex].CallAsync(CreateSetSlotNodePacket(slot, sourceNodeId));
                }
                catch (Exception exception)
                {
                    errors.Add(new InvalidOperationException(
                        "MOVED 清理阶段 node SETSLOT NODE 恢复失败。节点索引: " + nodeIndex +
                        "，端点: " + SeedEndpoints[nodeIndex] + "，原主节点: " + sourceNodeId,
                        exception));
                }
            }
        }

        return errors.Count == 0
            ? null
            : new Xunit.Sdk.XunitException("Redis Cluster MOVED 测试清理失败: " +
                string.Join(" | ", errors.Select(error => error.Message)));
    }

    /// <summary>
    /// 创建配置 JSON 序列化及单连接池的迁移测试客户端。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>配置完成的迁移测试 Redis 客户端。</returns>
    private static RedisClient CreateClient(string endpoint)
    {
        var builder = ConnectionStringBuilder.Parse(endpoint);
        // ASKING 只对同一条连接的下一条命令生效，迁移测试固定单连接池以保证清理顺序。
        builder.MinPoolSize = 1;
        builder.MaxPoolSize = 1;
        var client = new RedisClient(builder, Array.Empty<ConnectionStringBuilder>())
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        return client;
    }

    /// <summary>
    /// 创建连接到三个种子端点的 Redis Cluster 路由客户端。
    /// </summary>
    /// <returns>配置了三个种子节点的 Redis Cluster 路由客户端。</returns>
    private static RedisClient CreateClusterRouter()
    {
        var router = new RedisClient(SeedEndpoints
            .Select(endpoint => (ConnectionStringBuilder)endpoint)
            .ToArray())
        {
            Serialize = JsonConvert.SerializeObject,
            Deserialize = JsonConvert.DeserializeObject
        };
        return router;
    }

    /// <summary>
    /// 读取各 Redis 节点的节点标识。
    /// </summary>
    /// <param name="clients">Redis 节点客户端集合。</param>
    /// <returns>包含节点标识及其序号映射的异步任务。</returns>
    private static async Task<Dictionary<string, int>> ReadNodeIdsAsync(IReadOnlyList<RedisClient> clients)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < clients.Count; index++)
        {
            var response = await clients[index].CallAsync(new CommandPacket("CLUSTER", "MYID"));
            var nodeId = Convert.ToString(response, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(nodeId) || !result.TryAdd(nodeId, index))
                throw new InvalidOperationException("固定 Redis Cluster 种子未解析出唯一节点身份。");
        }
        return result;
    }

    /// <summary>
    /// 读取并解析 Redis Cluster 节点清单。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis Cluster 拓扑快照。</returns>
    /// <param name="discovery">拓扑发现结果。</param>
    private static async Task<ClusterTopologySnapshot> ReadTopologyAsync(RedisClient discovery)
    {
        var response = await discovery.CallAsync(new CommandPacket("CLUSTER", "NODES"));
        if (!(response is string nodes) || string.IsNullOrWhiteSpace(nodes))
            throw new InvalidOperationException("无法读取 Redis Cluster 节点清单。");
        return ParseTopology(nodes);
    }

    /// <summary>
    /// 解析 Redis Cluster 节点清单及各槽位的当前归属。
    /// </summary>
    /// <param name="nodes">Redis 节点集合。</param>
    /// <returns>解析出的主节点和槽位归属快照。</returns>
    private static ClusterTopologySnapshot ParseTopology(string nodes)
    {
        var masters = new List<ClusterMaster>();
        var owners = new string[ClusterSlotCount];
        foreach (var line in nodes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 8)
                throw new InvalidOperationException("Redis Cluster 节点清单格式无效。");

            var flags = fields[2].Split(',');
            if (!flags.Contains("master", StringComparer.Ordinal))
                continue;
            if (flags.Contains("fail", StringComparer.Ordinal) ||
                flags.Contains("fail?", StringComparer.Ordinal) ||
                !string.Equals(fields[7], "connected", StringComparison.Ordinal))
                throw new InvalidOperationException("Redis Cluster 主节点未处于 connected 状态。");

            var endpoint = fields[1].Split('@')[0];
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new InvalidOperationException("Redis Cluster 主节点 advertised endpoint 为空。");
            var master = new ClusterMaster(fields[0], endpoint);
            for (var index = 8; index < fields.Length; index++)
            {
                if (fields[index].StartsWith("[", StringComparison.Ordinal))
                    throw new InvalidOperationException("迁移测试要求开始时槽位不处于迁移中。");
                MarkSlotRange(fields[index], master, owners);
            }
            masters.Add(master);
        }

        if (masters.Count != SeedEndpoints.Length || owners.Any(owner => owner == null))
            throw new InvalidOperationException("Redis Cluster 必须由三个在线主节点完整覆盖 16384 个槽位。");
        return new ClusterTopologySnapshot(masters, owners);
    }

    /// <summary>
    /// 标记 Redis Cluster 槽位范围并检测重复覆盖。
    /// </summary>
    /// <param name="value">待处理值。</param>
    /// <param name="master">槽位所属主节点。</param>
    /// <param name="owners">各槽位当前所属主节点标识。</param>
    private static void MarkSlotRange(string value, ClusterMaster master, string[] owners)
    {
        var range = value.Split('-');
        if (range.Length > 2 || !int.TryParse(range[0], out var start))
            throw new InvalidOperationException("Redis Cluster 槽位范围无效。");
        var end = start;
        if (range.Length == 2 && !int.TryParse(range[1], out end))
            throw new InvalidOperationException("Redis Cluster 槽位范围无效。");
        if (start < 0 || end >= ClusterSlotCount || start > end)
            throw new InvalidOperationException("Redis Cluster 槽位范围超出有效边界。");
        for (var slot = start; slot <= end; slot++)
        {
            if (owners[slot] != null)
                throw new InvalidOperationException("Redis Cluster 槽位归属重复。");
            owners[slot] = master.Id;
            master.Slots.Add(slot);
        }
    }

    /// <summary>
    /// 选择同一槽位中的测试键。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="prefix">缓存键前缀。</param>
    /// <param name="topology">Redis Cluster 拓扑快照。</param>
    /// <returns>返回同一槽位的测试键集合。</returns>
    private static SelectedKeys SelectSameSlotKeys(CacheConfiguration configuration, string prefix,
        ClusterTopologySnapshot topology)
    {
        var items = new List<SelectedKey>();
        var selectedSlot = -1;
        for (var index = 0; index < 250_000 && items.Count < 4; index++)
        {
            var logicalKey = prefix + index.ToString(CultureInfo.InvariantCulture);
            var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);
            var slot = CacheClusterTopology.GetHashSlot(physicalKey);
            if (selectedSlot < 0)
                selectedSlot = slot;
            if (slot == selectedSlot)
                items.Add(new SelectedKey(logicalKey, physicalKey));
        }

        if (items.Count != 4 || !topology.Masters.Any(master => master.OwnsSlot(selectedSlot)))
            throw new InvalidOperationException("无法为迁移期测试找到同一 Redis 槽位的四个隔离测试键。");
        return new SelectedKeys(selectedSlot, items);
    }

    /// <summary>
    /// 创建用于迁移槽位状态的命令包。
    /// </summary>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="state">槽位状态。</param>
    /// <param name="nodeId">目标 Redis 节点标识。</param>
    /// <returns>用于设置槽位状态的 Redis 命令包。</returns>
    private static CommandPacket CreateSetSlotPacket(int slot, string state, string nodeId = null)
    {
        var arguments = nodeId == null
            ? new object[] { slot, state }
            : new object[] { slot, state, nodeId };
        return new CommandPacket("CLUSTER", "SETSLOT").Input(arguments);
    }

    /// <summary>
    /// 解析 Redis Cluster 通告端点中的主机名和端口。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <returns>返回端点主机和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string endpoint)
    {
        endpoint = endpoint?.Split('@')[0];
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || !int.TryParse(endpoint.Substring(separator + 1), out var port) || port <= 0)
            throw new InvalidOperationException("Redis Cluster advertised endpoint 缺少有效端口。");
        var host = endpoint.Substring(0, separator);
        if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
            host = host.Substring(1, host.Length - 2);
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Redis Cluster advertised endpoint 缺少有效主机名。");
        return (host, port);
    }

    /// <summary>
    /// 恢复 MOVED 测试中的槽位归属并删除迁移数据。
    /// </summary>
    /// <returns>异步操作，完成后返回恢复过程中捕获的异常；恢复成功时返回 null。</returns>
    /// <param name="clients">Redis 节点客户端集合。</param>
    /// <param name="router">Redis 路由客户端。</param>
    /// <param name="slot">Redis 槽位。</param>
    /// <param name="physicalKeys">物理缓存键集合。</param>
    /// <param name="migratedPhysicalKey">缓存键。</param>
    /// <param name="sourceClient">源客户端。</param>
    /// <param name="targetAdvertisedEndpoint">Redis 端点。</param>
    private static async Task<Exception> RestoreSlotAndRemoveKeysAsync(
        IReadOnlyList<RedisClient> clients, RedisClient router, int slot, IReadOnlyList<string> physicalKeys,
        string migratedPhysicalKey, RedisClient sourceClient, string targetAdvertisedEndpoint)
    {
        var errors = new List<Exception>();

        // 迁移标记仍在时，目标节点只接受带 ASKING 的命令；先清理已迁移键，避免 STABLE 后留下孤儿键。
        if (!string.IsNullOrWhiteSpace(migratedPhysicalKey) &&
            !string.IsNullOrWhiteSpace(targetAdvertisedEndpoint))
        {
            try
            {
                await DeleteMigratedKeyWithAskingAsync(targetAdvertisedEndpoint, migratedPhysicalKey);
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    "迁移清理阶段 target ASKING+DEL 失败。目标: " + targetAdvertisedEndpoint,
                    exception));
                if (sourceClient != null)
                {
                    try
                    {
                        // MIGRATE 失败时键可能仍在源节点；迁移中的缺失键会返回 ASK，随后由 STABLE 后兜底清理。
                        await sourceClient.CallAsync(new CommandPacket("DEL")
                            .Input(new object[] { migratedPhysicalKey }));
                    }
                    catch (Exception fallbackException)
                    {
                        errors.Add(new InvalidOperationException(
                            "迁移清理阶段 source migrated-key DEL fallback 失败。键: " + migratedPhysicalKey,
                            fallbackException));
                    }
                }
            }
        }

        // 源节点上的其余测试键也要在迁移标记仍在时删除；不使用 FLUSHDB。
        var sourceKeys = physicalKeys
            .Where(key => !string.Equals(key, migratedPhysicalKey, StringComparison.Ordinal))
            .ToArray();
        if (sourceKeys.Length > 0 && sourceClient != null)
        {
            // MIGRATING 状态下多键命令可能返回 TRYAGAIN；逐键删除才能避免跨迁移状态的批量请求。
            for (var sourceIndex = 0; sourceIndex < sourceKeys.Length; sourceIndex++)
            {
                var sourceKey = sourceKeys[sourceIndex];
                try
                {
                    await sourceClient.CallAsync(new CommandPacket("DEL")
                        .Input(new object[] { sourceKey }));
                }
                catch (Exception exception)
                {
                    errors.Add(new InvalidOperationException(
                        "迁移清理阶段 source key DEL 失败。索引: " + sourceIndex + "，键: " + sourceKey,
                        exception));
                }
            }
        }

        if (slot >= 0)
        {
            // 源和目标分别恢复 STABLE；其余主节点也执行一次以清理可能残留的本地迁移标记。
            for (var nodeIndex = 0; nodeIndex < clients.Count; nodeIndex++)
            {
                var client = clients[nodeIndex];
                try
                {
                    await client.CallAsync(CreateSetSlotPacket(slot, "STABLE"));
                }
                catch (Exception exception)
                {
                    errors.Add(new InvalidOperationException(
                        "迁移清理阶段 node STABLE 失败。节点索引: " + nodeIndex + "，端点: " +
                        SeedEndpoints[nodeIndex], exception));
                }
            }
        }

        // 路由器和逐节点删除是幂等的精确键清理，仅作为迁移标记恢复后的兜底。
        if (physicalKeys.Count > 0)
        {
            try
            {
                await router.DelAsync(physicalKeys.ToArray());
            }
            catch (Exception routerException)
            {
                var deleted = false;
                foreach (var client in clients)
                {
                    try
                    {
                        await client.CallAsync(new CommandPacket("DEL")
                            .Input(physicalKeys.Cast<object>().ToArray()));
                        deleted = true;
                        break;
                    }
                    catch
                    {
                        // 只有拥有测试槽位的主节点应当成功删除，继续尝试其余主节点。
                    }
                }
                if (!deleted)
                    errors.Add(new InvalidOperationException(
                        "迁移清理阶段 post-STABLE router/逐节点 DEL 失败。", routerException));
            }
        }

        return errors.Count == 0
            ? null
            : new Xunit.Sdk.XunitException("Redis Cluster 迁移测试清理失败: " +
                string.Join(" | ", errors.Select(error => error.Message)));
    }

    /// <summary>
    /// 通过同一 RESP 连接发送 ASKING 和 DEL，删除迁移到目标节点的测试键。
    /// </summary>
    /// <param name="endpoint">Redis 端点。</param>
    /// <param name="physicalKey">物理缓存键。</param>
    private static async Task DeleteMigratedKeyWithAskingAsync(string endpoint, string physicalKey)
    {
        var target = ParseEndpoint(endpoint);
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(target.Host, target.Port).ConfigureAwait(false);
        using var stream = tcpClient.GetStream();

        // ASKING 只影响同一条连接上的下一条命令，必须与 DEL 共用这个原始 RESP 会话。
        await WriteRespCommandAsync(stream, "ASKING").ConfigureAwait(false);
        var askingReply = await ReadRespLineAsync(stream).ConfigureAwait(false);
        if (!string.Equals(askingReply, "+OK", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Redis Cluster 目标节点 ASKING 回复异常: " + askingReply);

        await WriteRespCommandAsync(stream, "DEL", physicalKey).ConfigureAwait(false);
        var deleteReply = await ReadRespLineAsync(stream).ConfigureAwait(false);
        if (deleteReply.Length < 2 || deleteReply[0] != ':' ||
            !long.TryParse(deleteReply.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var deleted) || deleted < 0)
            throw new InvalidOperationException("Redis Cluster 目标节点 DEL 回复异常: " + deleteReply);
    }

    /// <summary>
    /// 将 RESP 命令编码后写入数据流。
    /// </summary>
    /// <param name="stream">数据流。</param>
    /// <param name="arguments">命令行参数。</param>
    private static async Task WriteRespCommandAsync(Stream stream, params string[] arguments)
    {
        var payload = new List<byte>();
        AppendUtf8(payload, "*" + arguments.Length.ToString(CultureInfo.InvariantCulture) + "\r\n");
        foreach (var argument in arguments)
        {
            var bytes = Encoding.UTF8.GetBytes(argument ?? string.Empty);
            AppendUtf8(payload, "$" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\r\n");
            payload.AddRange(bytes);
            AppendUtf8(payload, "\r\n");
        }

        var buffer = payload.ToArray();
        await stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 读取 RESP 行文本。
    /// </summary>
    /// <returns>异步操作，完成后返回读取到的 RESP 行文本。</returns>
    /// <param name="stream">数据流。</param>
    private static async Task<string> ReadRespLineAsync(Stream stream)
    {
        var line = new List<byte>();
        while (true)
        {
            var buffer = new byte[1];
            var read = await stream.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException("Redis Cluster 原始 RESP 连接提前关闭。");
            line.Add(buffer[0]);
            if (line.Count >= 2 && line[^2] == (byte)'\r' && line[^1] == (byte)'\n')
            {
                line.RemoveAt(line.Count - 1);
                line.RemoveAt(line.Count - 1);
                return Encoding.UTF8.GetString(line.ToArray());
            }
        }
    }

    /// <summary>
    /// 将 UTF-8 文本追加到协议缓冲区。
    /// </summary>
    /// <param name="destination">目标位置。</param>
    /// <param name="value">待处理值。</param>
    private static void AppendUtf8(List<byte> destination, string value)
    {
        destination.AddRange(Encoding.UTF8.GetBytes(value));
    }

    /// <summary>
    /// 断言测试夹具或缓存结果满足预期。
    /// </summary>
    /// <param name="exception">异常对象。</param>
    /// <param name="operation">待执行操作。</param>
    private static void AssertRedirectFailure(Exception exception, string operation)
    {
        var message = exception?.ToString() ?? string.Empty;
        Assert.True(message.Contains("ASK", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("MOVED", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("TRYAGAIN", StringComparison.OrdinalIgnoreCase),
            operation + " 必须显式传播 ASK/MOVED/TRYAGAIN 重定向错误，实际异常: " + message);
    }

    /// <summary>
    /// 保存 Redis Cluster 主节点集合及槽位归属。
    /// </summary>
    private sealed class ClusterTopologySnapshot
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterTopologySnapshot"/> 类型的实例。
        /// </summary>
    /// <param name="masters">主节点集合。</param>
    /// <param name="owners">槽位所属主节点标识。</param>
        public ClusterTopologySnapshot(IReadOnlyList<ClusterMaster> masters, IReadOnlyList<string> owners)
        {
            Masters = masters;
            Owners = owners;
        }

        /// <summary>
        /// 获取主节点集合。
        /// </summary>
        public IReadOnlyList<ClusterMaster> Masters { get; }
        /// <summary>
        /// 获取槽位所属主节点标识。
        /// </summary>
        private IReadOnlyList<string> Owners { get; }
    }

    /// <summary>
    /// 保存 Redis Cluster 主节点标识、端点和槽位集合。
    /// </summary>
    private sealed class ClusterMaster
    {
        /// <summary>
        /// 初始化一个 <see cref="ClusterMaster"/> 类型的实例。
        /// </summary>
    /// <param name="id">主节点标识。</param>
    /// <param name="endpoint">主节点端点。</param>
        public ClusterMaster(string id, string endpoint)
        {
            Id = id;
            Endpoint = endpoint;
        }

        /// <summary>
        /// 获取主节点标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取主节点端点。
        /// </summary>
        public string Endpoint { get; }
        /// <summary>
        /// 获取该测试节点负责的槽位集合。
        /// </summary>
        public HashSet<int> Slots { get; } = new();
        /// <summary>
        /// 判断主节点是否负责指定槽位。
        /// </summary>
        /// <param name="slot">Redis 槽位。</param>
    /// <returns>如果主节点负责该槽位，则返回 <see langword="true" />。</returns>
        public bool OwnsSlot(int slot) => Slots.Contains(slot);
    }

    /// <summary>
    /// 保存选定的槽位和测试键。
    /// </summary>
    private sealed class SelectedKeys
    {
        /// <summary>
        /// 初始化一个 <see cref="SelectedKeys"/> 类型的实例。
        /// </summary>
    /// <param name="slot">选定的 Redis 槽位。</param>
    /// <param name="items">槽位中的测试键集合。</param>
        public SelectedKeys(int slot, IReadOnlyList<SelectedKey> items)
        {
            Slot = slot;
            Items = items;
        }

        /// <summary>
        /// 获取选定的 Redis 槽位。
        /// </summary>
        public int Slot { get; }
        /// <summary>
        /// 获取槽位中的测试键集合。
        /// </summary>
        public IReadOnlyList<SelectedKey> Items { get; }
    }

    /// <summary>
    /// 保存测试键的逻辑键和物理键。
    /// </summary>
    private sealed class SelectedKey
    {
        /// <summary>
        /// 初始化一个 <see cref="SelectedKey"/> 类型的实例。
        /// </summary>
    /// <param name="logicalKey">逻辑缓存键。</param>
    /// <param name="physicalKey">物理缓存键。</param>
        public SelectedKey(string logicalKey, string physicalKey)
        {
            LogicalKey = logicalKey;
            PhysicalKey = physicalKey;
        }

        /// <summary>
        /// 获取逻辑缓存键。
        /// </summary>
        public string LogicalKey { get; }
        /// <summary>
        /// 获取物理缓存键。
        /// </summary>
        public string PhysicalKey { get; }
    }
}
