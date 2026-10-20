using Bing.Caching;
using EasyCaching.Core;
using EasyCaching.Core.Configurations;
using EasyCaching.Redis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using Xunit;

namespace Bing.EasyCaching.Tests;

/// <summary>
/// 客户端分片 Hybrid 组合故障测试门控。
/// </summary>
public sealed class EasyShardedHybridCombinedFaultFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedHybridCombinedFaultFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedHybridCombinedFaultFactAttribute()
    {
        var dataConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var shardConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        var busConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS");
        var restartContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var endpoints = new[] { dataConnection, shardConnection, busConnection }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_COMBINED_FAULT_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            endpoints.Length != 3 ||
            endpoints.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
        {
            Skip = "需要两个独立数据 Redis、一个独立总线 Redis，并显式设置 RUN_CACHE_COMBINED_FAULT_TESTS=true。";
            return;
        }

        if (!string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_PAUSE"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要显式设置 BING_TEST_ALLOW_REDIS_RESTART=true 和 BING_TEST_ALLOW_REDIS_PAUSE=true。";
            return;
        }

        if (string.IsNullOrWhiteSpace(restartContainer) ||
            !restartContainer.StartsWith("bing-cache-combined-", StringComparison.Ordinal))
            Skip = "需要 BING_TEST_RESTART_CONTAINER 指向 bing-cache-combined- 开头的临时容器。";
    }
}

/// <summary>
/// 验证客户端分片 Hybrid 组合故障后的恢复。
/// </summary>
/// <remarks>
/// 覆盖总线暂停与数据分片重启重叠场景，拒绝伪成功并恢复跨实例失效。
/// </remarks>
public sealed class EasyShardedHybridCombinedFaultIntegrationTest
{
    /// <summary>
    /// 总线暂停与分片重启重叠时拒绝待处理更新，并在重建 Provider 后恢复失效传播。
    /// </summary>
    [EasyShardedHybridCombinedFaultFact]
    public async Task ShardedHybrid_BusPauseOverlapsShardRestart_RejectsPendingUpdateAndRecovers()
    {
        var configuration = new CacheConfiguration(
            "combined-fault-" + Guid.NewGuid().ToString("N"),
            "combined-fault-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        var dataEndpoint = ParseEndpoint(FirstConnection);
        var busEndpoint = ParseEndpoint(BusConnection);
        var restartContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var targetPublishedPort = (await RunDockerAsync("port", restartContainer, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{dataEndpoint.Port}", targetPublishedPort);
        Assert.Contains(dataEndpoint.Host, new[] { "127.0.0.1", "localhost" });

        using var firstContainer = BuildHybrid(configuration);
        using var secondContainer = BuildHybrid(configuration);
        using var controlConnection = ConnectForControl(FirstConnection);
        using var busControl = ConnectForControl(BusConnection);
        var targetServer = controlConnection.GetServer(dataEndpoint.Host, dataEndpoint.Port);
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstDistributed = GetProvider(firstContainer, configuration, 0, "distributed");
        var secondLocal = GetProvider(secondContainer, configuration, 0, "local");
        var targetDatabase = Assert.IsAssignableFrom<IDatabase>(firstDistributed.Database);
        var targetKey = FindKeyForShard(configuration, 0);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, targetKey);
        var recoveryKey = FindKeyForShard(configuration, 0, 100);
        var recoveryPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, recoveryKey);
        var topic = configuration.ProviderName + ":shard:0:invalidations";
        var oldRunId = await ReadRunIdAsync(targetServer);
        Task pendingUpdate = null;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();

        try
        {
            await first.SetAsync(targetKey, 1);
            Assert.Equal(1, await second.GetAsync<int>(targetKey));
            Assert.Equal(1, secondLocal.Get<int>(physicalKey).Value);
            await WaitForSubscribersAsync(busServer, topic, 2);

            await busServer.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");
            pendingUpdate = first.SetAsync(targetKey, 2);
            await WaitUntilAsync(() =>
            {
                var committed = firstDistributed.Get<int>(physicalKey);
                return committed.HasValue && committed.Value == 2;
            }, "总线暂停期间未观察到分布式层提交更新。");
            await Task.Delay(100);
            Assert.False(pendingUpdate.IsCompleted,
                "分布式层已提交后，总线发布仍应保持在途以建立组合故障边界。");

            await RunDockerAsync("restart", "--time", "1", restartContainer);
            var newRunId = await WaitForRunIdChangeAsync(targetServer, oldRunId,
                TimeSpan.FromSeconds(30));
            Assert.NotEqual(oldRunId, newRunId);

            var pendingError = await Record.ExceptionAsync(async () =>
                await pendingUpdate.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(pendingError);
            Assert.IsNotType<TimeoutException>(pendingError);

            var oldManagerError = await WaitForIdentityErrorAsync(
                () => first.GetAsync<int>(targetKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", oldManagerError.Message, StringComparison.Ordinal);

            await WaitUntilAsync(async () =>
            {
                try
                {
                    await busServer.PingAsync();
                    return true;
                }
                catch (RedisTimeoutException) { return false; }
                catch (RedisConnectionException) { return false; }
            }, "Redis 总线暂停后未恢复响应。");

            using var rebuiltFirstContainer = BuildHybrid(configuration);
            using var rebuiltSecondContainer = BuildHybrid(configuration);
            var rebuiltFirst = rebuiltFirstContainer.GetRequiredService<IRedisCache>();
            var rebuiltSecond = rebuiltSecondContainer.GetRequiredService<IRedisCache>();
            await WaitForSubscribersAsync(busServer, topic, 4);

            secondLocal.Set(recoveryPhysicalKey, -1, TimeSpan.FromMinutes(5));
            Assert.Equal(-1, secondLocal.Get<int>(recoveryPhysicalKey).Value);
            await rebuiltFirst.SetAsync(recoveryKey, 4);
            await WaitUntilAsync(() => !secondLocal.Get<int>(recoveryPhysicalKey).HasValue,
                "总线恢复后的最终发布未使旧第二实例本地层失效。");
            Assert.Equal(4, await rebuiltSecond.GetAsync<int>(recoveryKey));
        }
        catch (Exception exception)
        {
            testError = exception;
        }
        finally
        {
            if (pendingUpdate != null)
            {
                try
                {
                    await pendingUpdate.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch
                {
                    // 组合故障预期可能使更新失败；主断言保留原始异常。
                }
            }

            try
            {
                await controlConnection.GetDatabase().KeyDeleteAsync(physicalKey);
                await controlConnection.GetDatabase().KeyDeleteAsync(recoveryPhysicalKey);
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }

            try
            {
                await busServer.ExecuteAsync("CLIENT", "PAUSE", "0", "ALL");
            }
            catch (Exception exception)
            {
                cleanupErrors.Add(exception);
            }
        }

        if (testError != null && cleanupErrors.Count > 0)
            testError.Data["CombinedFaultCleanupError"] = new AggregateException(cleanupErrors).ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupErrors.Count > 0)
            throw new AggregateException("客户端分片 Hybrid 组合故障测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 创建配置了客户端分片和 Hybrid 总线的服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <returns>返回已注册客户端分片 Hybrid Provider 的服务容器。</returns>
    private static ServiceProvider BuildHybrid(CacheConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedHybridCaching(configuration,
            new[] { ConfigureNode(FirstConnection), ConfigureNode(SecondConnection) },
            bus => bus.ConfigurationOptions = CreateBusOptions(BusConnection),
            configureHybrid: hybrid => hybrid.BusRetryCount = 0);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 配置测试 Redis 节点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回应用 Redis 节点连接设置的配置回调。</returns>
    private static Action<RedisOptions> ConfigureNode(string connection) => options =>
    {
        var endpoint = ParseEndpoint(connection);
        options.DBConfig.Endpoints.Add(new ServerEndPoint(endpoint.Host, endpoint.Port));
        options.DBConfig.AbortOnConnectFail = false;
    };

    /// <summary>
    /// 创建连接测试总线的 Redis 配置选项。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回测试总线使用的 Redis 连接选项。</returns>
    private static ConfigurationOptions CreateBusOptions(string connection)
    {
        var endpoint = ParseEndpoint(connection);
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            SyncTimeout = 750,
            AsyncTimeout = 750
        };
        options.EndPoints.Add(endpoint.Host, endpoint.Port);
        return options;
    }

    /// <summary>
    /// 获取指定缓存层级的 Provider。
    /// </summary>
    /// <param name="container">测试容器。</param>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="shard">分片。</param>
    /// <param name="tier">缓存层级。</param>
    /// <returns>返回指定缓存层和分片对应的 Provider。</returns>
    private static IEasyCachingProvider GetProvider(ServiceProvider container,
        CacheConfiguration configuration, int shard, string tier)
    {
        var factory = container.GetRequiredService<IEasyCachingProviderFactory>();
        return factory.GetCachingProvider(configuration.ProviderName + ":shard:" + shard + ":" + tier);
    }

    /// <summary>
    /// 查找由指定客户端分片负责的缓存键。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="expectedShard">预期分片。</param>
    /// <param name="startAt">起始时间。</param>
    /// <returns>返回映射到目标分片的逻辑键。</returns>
    private static string FindKeyForShard(CacheConfiguration configuration, int expectedShard,
        int startAt = 0)
    {
        for (var index = startAt; index < startAt + 10000; index++)
        {
            var key = "combined:item:" + index.ToString(CultureInfo.InvariantCulture);
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
    /// 执行指定的 Docker 控制命令并返回标准输出。
    /// </summary>
    /// <returns>异步操作，完成后返回 Docker 命令的标准输出。</returns>
    /// <param name="arguments">命令行参数。</param>
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
    /// 建立 Redis 控制连接。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回已启用管理命令的 Redis 连接多路复用器。</returns>
    private static ConnectionMultiplexer ConnectForControl(string connection)
    {
        var options = ConfigurationOptions.Parse(connection);
        options.AbortOnConnectFail = false;
        options.AllowAdmin = true;
        return ConnectionMultiplexer.Connect(options);
    }

    /// <summary>
    /// 读取 Redis 节点运行标识。
    /// </summary>
    /// <returns>异步操作，完成后返回 Redis 节点运行标识。</returns>
    /// <param name="server">Redis 服务端。</param>
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
    /// 等待 Redis 节点运行标识变化。
    /// </summary>
    /// <returns>异步操作，完成后返回变化后的 Redis 节点运行标识。</returns>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="oldRunId">旧运行标识。</param>
    /// <param name="timeout">超时时间。</param>
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
    /// 等待客户端分片身份检查抛出预期异常。
    /// </summary>
    /// <returns>异步操作，完成后返回捕获到的身份异常；未捕获异常时返回 null。</returns>
    /// <param name="operation">待执行操作。</param>
    /// <param name="timeout">超时时间。</param>
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
    /// 等待订阅者连接到消息总线。
    /// </summary>
    /// <param name="server">Redis 服务端。</param>
    /// <param name="topic">发布主题。</param>
    /// <param name="expected">预期值。</param>
    private static async Task WaitForSubscribersAsync(IServer server, string topic, long expected)
    {
        await WaitUntilAsync(async () =>
        {
            var result = (RedisResult[])await server.ExecuteAsync("PUBSUB", "NUMSUB", topic);
            return (long)result[1] >= expected;
        }, $"Redis 总线主题 {topic} 未达到 {expected} 个订阅。");
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<bool> condition, string error)
    {
        await WaitUntilAsync(() => Task.FromResult(condition()), error);
    }

    /// <summary>
    /// 等待条件满足或超时。
    /// </summary>
    /// <param name="condition">等待条件。</param>
    /// <param name="error">错误信息。</param>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string error)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!await condition())
        {
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(error);
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// 解析测试 Redis 连接字符串中的端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>返回 Redis 主机名和端口。</returns>
    private static (string Host, int Port) ParseEndpoint(string connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("缺少 Redis 测试连接配置。");
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
    /// 获取第一个 Redis 分片的连接字符串。
    /// </summary>
    private static string FirstConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");

    /// <summary>
    /// 获取第二个 Redis 分片的连接字符串。
    /// </summary>
    private static string SecondConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");

    /// <summary>
    /// 获取Hybrid 失效消息总线的 Redis 连接字符串。
    /// </summary>
    private static string BusConnection =>
        Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS");
}
