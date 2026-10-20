using Bing.Caching;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 客户端分片 Redis 重启测试门控。
/// </summary>
public class EasyShardedRedisRestartFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedRedisRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedRedisRestartFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要 RUN_INTEGRATION_TESTS=true、两个独立非生产 Redis 端点，并显式设置 BING_TEST_ALLOW_REDIS_RESTART=true。";
        }
    }
}

/// <summary>
/// 仅对显式指定的临时 Docker Redis 容器执行在途命令重启测试。
/// </summary>
public sealed class EasyShardedInFlightRestartFactAttribute : EasyShardedRedisRestartFactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedInFlightRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedInFlightRestartFactAttribute()
    {
        var container = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        if (string.IsNullOrWhiteSpace(container) ||
            !container.StartsWith("bing-cache-inflight-", StringComparison.Ordinal))
            Skip = "需要 BING_TEST_RESTART_CONTAINER 指向 bing-cache-inflight- 开头的临时容器。";
    }
}

/// <summary>
/// 验证客户端分片管理器在 Redis 节点重启后拒绝继续使用旧拓扑，并可通过重建恢复。
/// </summary>
public sealed class EasyShardedRestartIntegrationTest
{
    /// <summary>
    /// 验证第二批流读取期间节点重启会报告部分失败。
    /// </summary>
    [EasyShardedInFlightRestartFact]
    public async Task ShardedRedis_NodeRestartsDuringSecondBatch_StreamReportsPartialFailure()
    {
        var targetEndpoint = ParseEndpoint(FirstConnection);
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var publishedPort = (await RunDockerAsync("port", containerName, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{targetEndpoint.Port}", publishedPort);
        Assert.Contains(targetEndpoint.Host, new[] { "127.0.0.1", "localhost" });

        var configuration = new CacheConfiguration(
            "easy-sharded-batch-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-batch-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 1,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var serviceProvider = BuildCache(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        var server = controlConnection.GetServer(targetEndpoint.Host, targetEndpoint.Port);
        var cache = serviceProvider.GetRequiredService<IRedisCache>();
        var firstKey = FindKeyForShard(configuration, 0);
        var secondKey = FindKeyForShard(configuration, 0, startAt: 100);
        var physicalKeys = new[]
        {
            CacheKeyFormat.GetPhysicalKey(configuration, firstKey),
            CacheKeyFormat.GetPhysicalKey(configuration, secondKey)
        };

        try
        {
            await cache.SetAllAsync(new Dictionary<string, int>
            {
                [firstKey] = 101,
                [secondKey] = 202
            });
            var oldRunId = await ReadRunIdAsync(server);
            await using var stream = cache.GetAllStreamAsync<int>(new[] { firstKey, secondKey })
                .GetAsyncEnumerator();
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(firstKey, stream.Current.Key);
            Assert.Equal(101, stream.Current.Value);
            Assert.True(stream.Current.Hit);

            await server.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");
            var pendingSecondBatch = stream.MoveNextAsync().AsTask();
            await Task.Delay(250);
            Assert.False(pendingSecondBatch.IsCompleted);

            await RunDockerAsync("restart", "--time", "1", containerName);
            Assert.NotEqual(oldRunId,
                await WaitForRunIdChangeAsync(server, oldRunId, TimeSpan.FromSeconds(30)));
            var error = await Record.ExceptionAsync(async () =>
                await pendingSecondBatch.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(error);
            Assert.IsNotType<TimeoutException>(error);

            await WaitForIdentityErrorAsync(() => cache.GetAsync<int>(firstKey),
                TimeSpan.FromSeconds(30));
        }
        finally
        {
            var database = controlConnection.GetDatabase();
            foreach (var physicalKey in physicalKeys)
                await database.KeyDeleteAsync(physicalKey);
        }
    }

    /// <summary>
    /// 验证同步批量写入期间节点重启会报告部分失败。
    /// </summary>
    [EasyShardedInFlightRestartFact]
    public async Task ShardedRedis_NodeRestartsDuringSyncSetAll_ReportsPartialFailure()
    {
        var targetEndpoint = ParseEndpoint(FirstConnection);
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var publishedPort = (await RunDockerAsync("port", containerName, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{targetEndpoint.Port}", publishedPort);
        Assert.Contains(targetEndpoint.Host, new[] { "127.0.0.1", "localhost" });

        var configuration = new CacheConfiguration(
            "easy-sharded-sync-batch-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-sync-batch-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 1,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var serviceProvider = BuildCache(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        var server = controlConnection.GetServer(targetEndpoint.Host, targetEndpoint.Port);
        var database = controlConnection.GetDatabase();
        var cache = serviceProvider.GetRequiredService<IRedisCache>();
        var firstKey = FindKeyForShard(configuration, 0, startAt: 200);
        var secondKey = FindKeyForShard(configuration, 0, startAt: 10200);
        var firstPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, firstKey);
        var secondPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, secondKey);
        using var items = new BlockingBatchDictionary(
            new KeyValuePair<string, int>(firstKey, 101),
            new KeyValuePair<string, int>(secondKey, 202));

        var setAll = Task.Run(() => cache.SetAll(items));
        try
        {
            Assert.True(items.FirstBatchCompleted.Wait(TimeSpan.FromSeconds(30)),
                "同步 SetAll 未在限定时间内完成第一批写入。");
            Assert.True(await database.KeyExistsAsync(firstPhysicalKey),
                "第二批开始前第一批物理键不存在，无法证明边界已建立。");

            var oldRunId = await ReadRunIdAsync(server);
            await server.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");
            items.ReleaseSecondBatch.Set();

            await Task.Delay(250);
            Assert.False(setAll.IsCompleted,
                "CLIENT PAUSE 后同步 SetAll 已提前完成，未建立在途第二批写入。");

            await RunDockerAsync("restart", "--time", "1", containerName);
            Assert.NotEqual(oldRunId,
                await WaitForRunIdChangeAsync(server, oldRunId, TimeSpan.FromSeconds(30)));

            var setAllError = await Record.ExceptionAsync(async () =>
                await setAll.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(setAllError);
            Assert.IsNotType<TimeoutException>(setAllError);

            var oldManagerError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(firstKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", oldManagerError.Message, StringComparison.Ordinal);
        }
        finally
        {
            items.ReleaseSecondBatch.Set();
            try
            {
                await setAll.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch
            {
                // 保留主测试异常，同时避免阻塞测试进程退出。
            }

            await database.KeyDeleteAsync(firstPhysicalKey);
            await database.KeyDeleteAsync(secondPhysicalKey);
        }
    }

    /// <summary>
    /// 验证异步批量写入期间节点重启会报告部分失败。
    /// </summary>
    [EasyShardedInFlightRestartFact]
    public async Task ShardedRedis_NodeRestartsDuringAsyncSetAll_ReportsPartialFailure()
    {
        var targetEndpoint = ParseEndpoint(FirstConnection);
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var publishedPort = (await RunDockerAsync("port", containerName, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{targetEndpoint.Port}", publishedPort);
        Assert.Contains(targetEndpoint.Host, new[] { "127.0.0.1", "localhost" });

        var configuration = new CacheConfiguration(
            "easy-sharded-async-batch-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-async-batch-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 1,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var serviceProvider = BuildCache(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        var server = controlConnection.GetServer(targetEndpoint.Host, targetEndpoint.Port);
        var database = controlConnection.GetDatabase();
        var cache = serviceProvider.GetRequiredService<IRedisCache>();
        var firstKey = FindKeyForShard(configuration, 0, startAt: 20200);
        var secondKey = FindKeyForShard(configuration, 0, startAt: 30200);
        var firstPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, firstKey);
        var secondPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, secondKey);
        using var items = new BlockingBatchDictionary(
            new KeyValuePair<string, int>(firstKey, 101),
            new KeyValuePair<string, int>(secondKey, 202));

        var setAll = Task.Run(() => cache.SetAllAsync(items));
        try
        {
            Assert.True(items.FirstBatchCompleted.Wait(TimeSpan.FromSeconds(30)),
                "异步 SetAll 未在限定时间内完成第一批写入。");
            Assert.True(await database.KeyExistsAsync(firstPhysicalKey),
                "第二批开始前第一批物理键不存在，无法证明边界已建立。");

            var oldRunId = await ReadRunIdAsync(server);
            await server.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");
            items.ReleaseSecondBatch.Set();

            await Task.Delay(250);
            Assert.False(setAll.IsCompleted,
                "CLIENT PAUSE 后异步 SetAll 已提前完成，未建立在途第二批写入。");

            await RunDockerAsync("restart", "--time", "1", containerName);
            Assert.NotEqual(oldRunId,
                await WaitForRunIdChangeAsync(server, oldRunId, TimeSpan.FromSeconds(30)));

            var setAllError = await Record.ExceptionAsync(async () =>
                await setAll.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(setAllError);
            Assert.IsNotType<TimeoutException>(setAllError);

            var oldManagerError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(firstKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", oldManagerError.Message, StringComparison.Ordinal);
        }
        finally
        {
            items.ReleaseSecondBatch.Set();
            try
            {
                await setAll.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch
            {
                // 保留主测试异常，同时避免阻塞测试进程退出。
            }

            await database.KeyDeleteAsync(firstPhysicalKey);
            await database.KeyDeleteAsync(secondPhysicalKey);
        }
    }

    /// <summary>
    /// 验证节点重启期间的在途读取不会报告成功。
    /// </summary>
    [EasyShardedInFlightRestartFact]
    public async Task ShardedRedis_NodeRestartsDuringRead_InFlightReadDoesNotReturnSuccess()
    {
        var targetEndpoint = ParseEndpoint(FirstConnection);
        var containerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var publishedPort = (await RunDockerAsync("port", containerName, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{targetEndpoint.Port}", publishedPort);
        Assert.Contains(targetEndpoint.Host, new[] { "127.0.0.1", "localhost" });

        var configuration = new CacheConfiguration(
            "easy-sharded-inflight-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-inflight-" + Guid.NewGuid().ToString("N"),
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var serviceProvider = BuildCache(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        var server = controlConnection.GetServer(targetEndpoint.Host, targetEndpoint.Port);
        var cache = serviceProvider.GetRequiredService<IRedisCache>();
        var key = FindKeyForShard(configuration, 0);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);

        try
        {
            await cache.SetAsync(key, 101);
            var oldRunId = await ReadRunIdAsync(server);
            await server.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");

            var pendingRead = cache.GetAsync<int>(key);
            await Task.Delay(250);
            Assert.False(pendingRead.IsCompleted);

            await RunDockerAsync("restart", "--time", "1", containerName);
            Assert.NotEqual(oldRunId,
                await WaitForRunIdChangeAsync(server, oldRunId, TimeSpan.FromSeconds(30)));
            var inFlightError = await Record.ExceptionAsync(async () =>
                await pendingRead.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(inFlightError);
            Assert.IsNotType<TimeoutException>(inFlightError);

            var oldManagerError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(key), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", oldManagerError.Message, StringComparison.Ordinal);

            using var rebuiltProvider = BuildCache(configuration);
            var rebuiltCache = rebuiltProvider.GetRequiredService<IRedisCache>();
            await rebuiltCache.SetAsync(key, 303);
            Assert.Equal(303, await rebuiltCache.GetAsync<int>(key));
        }
        finally
        {
            await controlConnection.GetDatabase().KeyDeleteAsync(physicalKey);
        }
    }

    /// <summary>
    /// 验证节点重启后旧管理器拒绝操作且重建后的管理器恢复访问。
    /// </summary>
    [EasyShardedRedisRestartFact]
    public async Task ShardedRedis_NodeRestartChangesIdentity_OldManagerRejectsAndRebuiltManagerRecovers()
    {
        var configuration = new CacheConfiguration(
            "easy-sharded-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);

        using var container = BuildCache(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        using var otherConnection = ConnectForControl(SecondConnection);
        var targetEndpoint = ParseEndpoint(FirstConnection);
        var targetServer = controlConnection.GetServer(targetEndpoint.Host, targetEndpoint.Port);
        var targetDatabase = controlConnection.GetDatabase();
        var otherDatabase = otherConnection.GetDatabase();
        var cache = container.GetRequiredService<IRedisCache>();
        var targetKey = FindKeyForShard(configuration, 0);
        var otherKey = FindKeyForShard(configuration, 1);
        var physicalTargetKey = CacheKeyFormat.GetPhysicalKey(configuration, targetKey);
        var physicalOtherKey = CacheKeyFormat.GetPhysicalKey(configuration, otherKey);

        try
        {
            await cache.SetAsync(targetKey, 101);
            await cache.SetAsync(otherKey, 202);
            Assert.Equal(101, await cache.GetAsync<int>(targetKey));

            var oldRunId = await ReadRunIdAsync(targetServer);
            await RestartAsync(targetServer);
            var newRunId = await WaitForRunIdChangeAsync(targetServer, oldRunId,
                TimeSpan.FromSeconds(30));
            Assert.NotEqual(oldRunId, newRunId);

            var singleError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(targetKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", singleError.Message, StringComparison.Ordinal);

            var batchError = await WaitForIdentityErrorAsync(
                () => cache.GetAllAsync<int>(new[] { targetKey, otherKey }),
                TimeSpan.FromSeconds(30));
            Assert.Contains("身份", batchError.Message, StringComparison.Ordinal);

            using var rebuiltContainer = BuildCache(configuration);
            var rebuiltCache = rebuiltContainer.GetRequiredService<IRedisCache>();
            await rebuiltCache.SetAsync(targetKey, 303);
            Assert.Equal(303, await rebuiltCache.GetAsync<int>(targetKey));
        }
        finally
        {
            await targetDatabase.KeyDeleteAsync(physicalTargetKey);
            await otherDatabase.KeyDeleteAsync(physicalOtherKey);
        }
    }

    /// <summary>
    /// 重启 Redis 节点并处理重启期间的连接异常。
    /// </summary>
    /// <param name="server">要重启的 Redis 服务器。</param>
    private static async Task RestartAsync(IServer server)
    {
        try
        {
            // Redis 7.4 的 DEBUG RESTART 参数是重启延迟毫秒数；测试只对显式隔离的节点执行。
            await server.ExecuteAsync("DEBUG", "RESTART", "0");
        }
        catch (RedisServerException error)
        {
            throw new InvalidOperationException(
                "目标 Redis 未允许 DEBUG RESTART；请仅对启用 enable-debug-command yes 的临时非生产节点运行此测试。",
                error);
        }
        catch (RedisConnectionException)
        {
            // DEBUG RESTART 通常会在命令完成时主动断开当前连接。
        }
        catch (RedisTimeoutException)
        {
            // 服务器重启期间命令响应可能超时，后续通过新的 run_id 确认重启完成。
        }
    }

    /// <summary>
    /// 运行 Docker 命令并返回标准输出。
    /// </summary>
    /// <param name="arguments">Docker 命令及其参数。</param>
    /// <returns>Docker 命令的标准输出。</returns>
    private static async Task<string> RunDockerAsync(params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("docker")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start())
            throw new InvalidOperationException("无法启动 Docker 命令。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Docker 命令失败：{await error}");
        return await output;
    }

    /// <summary>
    /// 在第二批数据枚举时阻塞的测试字典。
    /// </summary>
    private sealed class BlockingBatchDictionary : IDictionary<string, int>, IDisposable
    {
        /// <summary>
        /// 批量枚举时首先返回的键值对。
        /// </summary>
        private readonly KeyValuePair<string, int> _first;
        /// <summary>
        /// 批量枚举时第二个返回的键值对。
        /// </summary>
        private readonly KeyValuePair<string, int> _second;

        /// <summary>
        /// 初始化一个 <see cref="BlockingBatchDictionary"/> 类型的实例。
        /// </summary>
        /// <param name="first">首批返回的键值对。</param>
        /// <param name="second">释放阻塞后返回的键值对。</param>
        internal BlockingBatchDictionary(KeyValuePair<string, int> first,
            KeyValuePair<string, int> second)
        {
            _first = first;
            _second = second;
        }

        /// <summary>
        /// 获取首批数据已枚举完成的信号。
        /// </summary>
        internal ManualResetEventSlim FirstBatchCompleted { get; } = new();
        /// <summary>
        /// 获取用于释放第二批枚举的信号。
        /// </summary>
        internal ManualResetEventSlim ReleaseSecondBatch { get; } = new();

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator()
        {
            yield return _first;
            FirstBatchCompleted.Set();
            if (!ReleaseSecondBatch.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("测试未在限定时间内释放同步 SetAll 的第二批。");
            yield return _second;
        }

        /// <inheritdoc />
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        /// <summary>
        /// 获取字典中的键值对数量。
        /// </summary>
        public int Count => 2;
        /// <summary>
        /// 获取字典是否只读。
        /// </summary>
        public bool IsReadOnly => true;
        /// <summary>
        /// 获取字典中的键集合。
        /// </summary>
        public ICollection<string> Keys => new[] { _first.Key, _second.Key };
        /// <summary>
        /// 获取字典中的值集合。
        /// </summary>
        public ICollection<int> Values => new[] { _first.Value, _second.Value };

        /// <inheritdoc />
        public int this[string key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new KeyNotFoundException(key);
            }
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public void Add(string key, int value) => throw new NotSupportedException();
        /// <inheritdoc />
        public bool ContainsKey(string key) => key == _first.Key || key == _second.Key;
        /// <inheritdoc />
        public bool Remove(string key) => throw new NotSupportedException();
        /// <inheritdoc />
        public bool TryGetValue(string key, out int value)
        {
            if (key == _first.Key)
            {
                value = _first.Value;
                return true;
            }
            if (key == _second.Key)
            {
                value = _second.Value;
                return true;
            }
            value = default;
            return false;
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<string, int> item) => throw new NotSupportedException();
        /// <inheritdoc />
        public void Clear() => throw new NotSupportedException();
        /// <inheritdoc />
        public bool Contains(KeyValuePair<string, int> item) =>
            TryGetValue(item.Key, out var value) && value == item.Value;
        /// <inheritdoc />
        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || array.Length - arrayIndex < Count)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            array[arrayIndex] = _first;
            array[arrayIndex + 1] = _second;
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<string, int> item) => throw new NotSupportedException();

        /// <inheritdoc />
        public void Dispose()
        {
            ReleaseSecondBatch.Set();
            FirstBatchCompleted.Dispose();
            ReleaseSecondBatch.Dispose();
        }
    }

    /// <summary>
    /// 等待 Redis 节点报告新的运行标识。
    /// </summary>
    /// <param name="server">要查询的 Redis 服务器。</param>
    /// <param name="oldRunId">重启前的运行标识。</param>
    /// <param name="timeout">等待新标识的最长时间。</param>
    /// <returns>重启后服务器报告的运行标识。</returns>
    private static async Task<string> WaitForRunIdChangeAsync(IServer server, string oldRunId,
        TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var current = await ReadRunIdAsync(server);
                if (!string.Equals(current, oldRunId, StringComparison.Ordinal))
                    return current;
            }
            catch (RedisConnectionException)
            {
                // 等待 StackExchange.Redis 自动重连。
            }
            catch (RedisTimeoutException)
            {
                // 等待重启后的 INFO 响应恢复。
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("Redis 节点重启后未在限定时间内观察到新的 run_id。");
    }

    /// <summary>
    /// 读取 Redis 服务器当前的运行标识。
    /// </summary>
    /// <param name="server">要查询的 Redis 服务器。</param>
    /// <returns>服务器报告的运行标识。</returns>
    private static async Task<string> ReadRunIdAsync(IServer server)
    {
        var info = (await server.ExecuteAsync("INFO", "SERVER")).ToString();
        foreach (var line in info.Split('\n'))
        {
            if (!line.StartsWith("run_id:", StringComparison.Ordinal))
                continue;
            var runId = line["run_id:".Length..].Trim();
            if (!string.IsNullOrEmpty(runId))
                return runId;
        }

        throw new InvalidOperationException("Redis INFO SERVER 响应中缺少 run_id。");
    }

    /// <summary>
    /// 等待操作报告分片身份变化错误。
    /// </summary>
    /// <param name="operation">要重复执行并检查的异步操作。</param>
    /// <param name="timeout">等待身份错误的最长时间。</param>
    /// <returns>操作报告的分片身份变化异常。</returns>
    private static async Task<InvalidOperationException> WaitForIdentityErrorAsync(
        Func<Task> operation, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        Exception lastError = null;
        while (stopwatch.Elapsed < timeout)
        {
            var error = await Record.ExceptionAsync(operation);
            if (error is InvalidOperationException invalidOperation &&
                invalidOperation.Message.Contains("身份", StringComparison.Ordinal))
                return invalidOperation;
            lastError = error;
            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException(
            $"旧分片管理器未在限定时间内报告 Redis 身份变化。最后错误：{lastError}");
    }

    /// <summary>
    /// 创建允许执行管理命令的 Redis 控制连接。
    /// </summary>
    /// <param name="connection">Redis 连接字符串。</param>
    /// <returns>已连接的 Redis 多路复用器。</returns>
    private static ConnectionMultiplexer ConnectForControl(string connection)
    {
        var options = ConfigurationOptions.Parse(connection);
        options.AbortOnConnectFail = false;
        options.AllowAdmin = true;
        return ConnectionMultiplexer.Connect(options);
    }

    /// <summary>
    /// 注册并构建客户端分片缓存服务。
    /// </summary>
    /// <param name="configuration">客户端分片缓存配置。</param>
    /// <returns>包含分片缓存服务的服务提供器。</returns>
    private static ServiceProvider BuildCache(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedRedisCaching(configuration, new[]
        {
            ConfigureNode(FirstConnection),
            ConfigureNode(SecondConnection)
        });
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 配置单个 Redis 分片的连接端点。
    /// </summary>
    /// <param name="connection">Redis 连接字符串。</param>
    /// <returns>可应用于 Redis 分片 Provider 的配置委托。</returns>
    private static Action<RedisOptions> ConfigureNode(string connection) => options =>
    {
        var endpoint = ParseEndpoint(connection);
        options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port));
        options.DBConfig.AbortOnConnectFail = false;
    };

    /// <summary>
    /// 查找经物理键哈希后路由到指定分片的逻辑键。
    /// </summary>
    /// <param name="configuration">用于生成物理键的缓存配置。</param>
    /// <param name="expectedShard">目标分片的零基索引。</param>
    /// <param name="startAt">搜索的起始编号。</param>
    /// <returns>路由到目标分片的逻辑键。</returns>
    private static string FindKeyForShard(CacheConfiguration configuration, int expectedShard,
        int startAt = 0)
    {
        for (var index = startAt; index < startAt + 10000; index++)
        {
            var key = "restart:" + index;
            var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);
            var start = physicalKey.IndexOf('{') + 1;
            if (start <= 0 || start + 8 > physicalKey.Length)
                continue;
            var hash = uint.Parse(physicalKey.Substring(start, 8), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            if (hash % 2 == expectedShard)
                return key;
        }

        throw new InvalidOperationException("无法生成目标客户端分片测试键。");
    }

    /// <summary>
    /// 从 Redis 连接字符串中解析第一个主机和端口。
    /// </summary>
    /// <param name="connection">包含 Redis 端点的连接字符串。</param>
    /// <returns>解析出的主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        var endpoint = connection.Split(',', 2)[0].Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1 ||
            !int.TryParse(endpoint[(separator + 1)..], out var port))
            throw new FormatException("Redis 连接串必须包含 host:port 端点。");
        var host = endpoint[..separator].Trim();
        if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
            host = host[1..^1];
        return (host, port);
    }

    /// <summary>
    /// 获取主 Redis 分片的测试连接字符串。
    /// </summary>
    private static string FirstConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");

    /// <summary>
    /// 获取第二个 Redis 分片的测试连接字符串。
    /// </summary>
    private static string SecondConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
}
