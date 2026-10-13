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
/// 客户端分片 Hybrid 双节点与总线组合故障测试门控。
/// </summary>
public sealed class EasyShardedHybridTwoNodeBusFaultFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedHybridTwoNodeBusFaultFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedHybridTwoNodeBusFaultFactAttribute()
    {
        var firstConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS");
        var secondConnection = Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION");
        var busConnection = Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_BUS_REDIS");
        var endpoints = new[] { firstConnection, secondConnection, busConnection };

        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_TRIPLE_FAULT_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            endpoints.Any(connection => !TryParseLoopbackEndpoint(connection)) ||
            !HaveDistinctEndpoints(endpoints))
        {
            Skip = "需要三个不同的本机 Redis 端点，并显式设置 RUN_CACHE_TRIPLE_FAULT_TESTS=true。";
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

        var firstContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var secondContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        if (!IsIsolatedContainerName(firstContainer) ||
            !IsIsolatedContainerName(secondContainer) ||
            string.Equals(firstContainer, secondContainer, StringComparison.Ordinal))
        {
            Skip = "需要两个不同的 bing-cache-triple- 开头临时容器。";
        }
    }

    /// <summary>
    /// 判断节点端点是否全部不同。
    /// </summary>
    /// <param name="connections">待比较的 Redis 连接字符串集合。</param>
    /// <returns>如果所有连接指向不同端点，则返回 true。</returns>
    private static bool HaveDistinctEndpoints(IReadOnlyList<string> connections)
    {
        var parsed = connections.Select(ParseEndpoint).ToArray();
        return parsed.Select(endpoint => endpoint.Host.ToLowerInvariant() + ":" + endpoint.Port)
            .Distinct(StringComparer.Ordinal)
            .Count() == parsed.Length;
    }

    /// <summary>
    /// 解析回环 Redis 端点。
    /// </summary>
    /// <param name="connection">Redis 连接。</param>
    /// <returns>如果连接字符串解析为回环地址，则返回 true。</returns>
    private static bool TryParseLoopbackEndpoint(string connection)
    {
        try
        {
            var endpoint = ParseEndpoint(connection);
            return IsLoopbackHost(endpoint.Host) && endpoint.Port > 0 && endpoint.Port <= 65535;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断容器名称是否符合隔离测试规则。
    /// </summary>
    /// <param name="name">待检查的 Docker 容器名称。</param>
    /// <returns>如果容器名符合隔离测试规则，则返回 true。</returns>
    private static bool IsIsolatedContainerName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.StartsWith("bing-cache-triple-", StringComparison.Ordinal) &&
        name.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '/', '\\' }) < 0;

    /// <summary>
    /// 判断主机是否为回环地址。
    /// </summary>
    /// <param name="host">服务主机。</param>
    /// <returns>如果主机是本地回环地址，则返回 true。</returns>
    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);

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
}

/// <summary>
/// 验证客户端分片 Hybrid 双分片故障后的恢复。
/// </summary>
/// <remarks>
/// 覆盖总线暂停与两个数据分片同时重启，拒绝伪成功并恢复跨实例本地失效。
/// </remarks>
public sealed class EasyShardedHybridTwoNodeBusFaultIntegrationTest
{
    /// <summary>
    /// 总线暂停与两个分片同时重启重叠时拒绝待处理更新，并在恢复后重新建立失效传播。
    /// </summary>
    [EasyShardedHybridTwoNodeBusFaultFact]
    public async Task ShardedHybrid_BusPauseOverlapsBothShardRestarts_RejectsPendingUpdateAndRecovers()
    {
        var firstConnection = FirstConnection;
        var secondConnection = SecondConnection;
        var busConnection = BusConnection;
        var firstContainerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var secondContainerName = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        var firstEndpoint = ParseEndpoint(firstConnection);
        var secondEndpoint = ParseEndpoint(secondConnection);
        var busEndpoint = ParseEndpoint(busConnection);

        Assert.True(IsLoopbackHost(firstEndpoint.Host));
        Assert.True(IsLoopbackHost(secondEndpoint.Host));
        Assert.True(IsLoopbackHost(busEndpoint.Host));
        Assert.False(AreSameEndpoint(firstEndpoint, secondEndpoint));
        Assert.False(AreSameEndpoint(firstEndpoint, busEndpoint));
        Assert.False(AreSameEndpoint(secondEndpoint, busEndpoint));
        await ValidateIsolatedContainerAsync(firstContainerName, firstEndpoint);
        await ValidateIsolatedContainerAsync(secondContainerName, secondEndpoint);

        var configuration = new CacheConfiguration(
            "triple-fault-" + Guid.NewGuid().ToString("N"),
            "triple-fault-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var firstContainer = BuildHybrid(configuration);
        using var secondContainer = BuildHybrid(configuration);
        using var firstControl = ConnectForControl(firstConnection);
        using var secondControl = ConnectForControl(secondConnection);
        using var busControl = ConnectForControl(busConnection);

        var firstServer = firstControl.GetServer(firstEndpoint.Host, firstEndpoint.Port);
        var secondServer = secondControl.GetServer(secondEndpoint.Host, secondEndpoint.Port);
        var busServer = busControl.GetServer(busEndpoint.Host, busEndpoint.Port);
        var firstDatabase = firstControl.GetDatabase();
        var secondDatabase = secondControl.GetDatabase();
        var first = firstContainer.GetRequiredService<IRedisCache>();
        var second = secondContainer.GetRequiredService<IRedisCache>();
        var firstDistributed = GetProvider(firstContainer, configuration, 0, "distributed");
        var firstLocal = GetProvider(firstContainer, configuration, 0, "local");
        var secondFirstLocal = GetProvider(secondContainer, configuration, 0, "local");
        var secondSecondLocal = GetProvider(secondContainer, configuration, 1, "local");

        var firstKey = FindKeyForShard(configuration, 0);
        var secondKey = FindKeyForShard(configuration, 1);
        var firstPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, firstKey);
        var secondPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, secondKey);
        var firstRecoveryKey = FindKeyForShard(configuration, 0, 100);
        var secondRecoveryKey = FindKeyForShard(configuration, 1, 100);
        var firstRecoveryPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, firstRecoveryKey);
        var secondRecoveryPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, secondRecoveryKey);
        var firstTopic = configuration.ProviderName + ":shard:0:invalidations";
        var secondTopic = configuration.ProviderName + ":shard:1:invalidations";
        var oldRunIds = await Task.WhenAll(ReadRunIdAsync(firstServer), ReadRunIdAsync(secondServer));
        Task pendingUpdate = null;
        Exception testError = null;
        var cleanupErrors = new List<Exception>();

        try
        {
            await WaitForSubscribersAsync(busServer, firstTopic, 2);
            await WaitForSubscribersAsync(busServer, secondTopic, 2);

            await first.SetAsync(firstKey, 101);
            await first.SetAsync(secondKey, 202);
            Assert.Equal(101, await second.GetAsync<int>(firstKey));
            Assert.Equal(202, await second.GetAsync<int>(secondKey));
            Assert.Equal(101, firstLocal.Get<int>(firstPhysicalKey).Value);
            Assert.Equal(101, secondFirstLocal.Get<int>(firstPhysicalKey).Value);
            Assert.Equal(202, secondSecondLocal.Get<int>(secondPhysicalKey).Value);

            await busServer.ExecuteAsync("CLIENT", "PAUSE", "15000", "ALL");
            pendingUpdate = first.SetAsync(firstKey, 303);
            await WaitUntilAsync(() =>
            {
                var committed = firstDistributed.Get<int>(firstPhysicalKey);
                return committed.HasValue && committed.Value == 303;
            }, "总线暂停期间未观察到第一个分片的分布式层提交更新。");
            await Task.Delay(100);
            Assert.False(pendingUpdate.IsCompleted,
                "分布式层已提交后，总线发布仍应保持在途以建立双分片组合故障边界。");

            // 两个重启在同一个总线暂停窗口内发出，避免退化为两个独立故障测试。
            await Task.WhenAll(
                RunDockerAsync("restart", "--time", "1", firstContainerName),
                RunDockerAsync("restart", "--time", "1", secondContainerName));

            var newRunIds = await Task.WhenAll(
                WaitForRunIdChangeAsync(firstServer, oldRunIds[0], TimeSpan.FromSeconds(30)),
                WaitForRunIdChangeAsync(secondServer, oldRunIds[1], TimeSpan.FromSeconds(30)));
            Assert.NotEqual(oldRunIds[0], newRunIds[0]);
            Assert.NotEqual(oldRunIds[1], newRunIds[1]);

            var pendingError = await Record.ExceptionAsync(async () =>
                await pendingUpdate.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.NotNull(pendingError);
            Assert.IsNotType<TimeoutException>(pendingError);

            var firstManagerError = await WaitForIdentityErrorAsync(
                () => first.GetAsync<int>(firstKey), TimeSpan.FromSeconds(30));
            var secondManagerError = await WaitForIdentityErrorAsync(
                () => first.GetAsync<int>(secondKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", firstManagerError.Message, StringComparison.Ordinal);
            Assert.Contains("身份", secondManagerError.Message, StringComparison.Ordinal);

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
            var rebuiltFirstTopicWait = WaitForSubscribersAsync(busServer, firstTopic, 4);
            var rebuiltSecondTopicWait = WaitForSubscribersAsync(busServer, secondTopic, 4);
            await Task.WhenAll(rebuiltFirstTopicWait, rebuiltSecondTopicWait);

            // 恢复键与暂停期间的在途键完全不同，避免旧消息抵达时造成误判。
            secondFirstLocal.Set(firstRecoveryPhysicalKey, -1, TimeSpan.FromMinutes(5));
            secondSecondLocal.Set(secondRecoveryPhysicalKey, -1, TimeSpan.FromMinutes(5));
            Assert.Equal(-1, secondFirstLocal.Get<int>(firstRecoveryPhysicalKey).Value);
            Assert.Equal(-1, secondSecondLocal.Get<int>(secondRecoveryPhysicalKey).Value);

            await rebuiltFirst.SetAsync(firstRecoveryKey, 404);
            await WaitUntilAsync(() => !secondFirstLocal.Get<int>(firstRecoveryPhysicalKey).HasValue,
                "第一个分片恢复后的失效通知未清理旧第二实例本地层。");
            await rebuiltFirst.SetAsync(secondRecoveryKey, 505);
            await WaitUntilAsync(() => !secondSecondLocal.Get<int>(secondRecoveryPhysicalKey).HasValue,
                "第二个分片恢复后的失效通知未清理旧第二实例本地层。");
            Assert.Equal(404, await rebuiltSecond.GetAsync<int>(firstRecoveryKey));
            Assert.Equal(505, await rebuiltSecond.GetAsync<int>(secondRecoveryKey));
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

            await TryDeleteAsync(firstDatabase, firstPhysicalKey, cleanupErrors);
            await TryDeleteAsync(firstDatabase, firstPhysicalKey + ":lease", cleanupErrors);
            await TryDeleteAsync(firstDatabase, firstRecoveryPhysicalKey, cleanupErrors);
            await TryDeleteAsync(firstDatabase, firstRecoveryPhysicalKey + ":lease", cleanupErrors);
            await TryDeleteAsync(secondDatabase, secondPhysicalKey, cleanupErrors);
            await TryDeleteAsync(secondDatabase, secondPhysicalKey + ":lease", cleanupErrors);
            await TryDeleteAsync(secondDatabase, secondRecoveryPhysicalKey, cleanupErrors);
            await TryDeleteAsync(secondDatabase, secondRecoveryPhysicalKey + ":lease", cleanupErrors);

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
            testError.Data["TripleFaultCleanupError"] = new AggregateException(cleanupErrors).ToString();
        if (testError != null)
            ExceptionDispatchInfo.Capture(testError).Throw();
        if (cleanupErrors.Count > 0)
            throw new AggregateException("客户端分片 Hybrid 双节点总线故障测试清理失败。", cleanupErrors);
    }

    /// <summary>
    /// 创建配置了两个 Redis 分片和 Hybrid 总线的服务容器。
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
    /// 为指定 Redis 节点创建连接配置回调。
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
    /// 创建连接指定端点的总线 Redis 配置选项。
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
            var key = "triple:item:" + index.ToString(CultureInfo.InvariantCulture);
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
    /// 验证 Docker 容器和 Redis 端点符合隔离测试环境要求。
    /// </summary>
    /// <param name="container">测试容器。</param>
    /// <param name="endpoint">Redis 端点。</param>
    private static async Task ValidateIsolatedContainerAsync(string container,
        (string Host, int Port) endpoint)
    {
        Assert.True(IsIsolatedContainerName(container));
        var inspect = (await RunDockerAsync("inspect", "--format", "{{.Name}}|{{.State.Running}}",
            container)).Trim();
        Assert.Equal("/" + container + "|true", inspect);
        var publishedPort = (await RunDockerAsync("port", container, "6379/tcp")).Trim();
        Assert.Equal("127.0.0.1:" + endpoint.Port, publishedPort);
        Assert.True(IsLoopbackHost(endpoint.Host));
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
    /// 建立允许管理命令的 Redis 控制连接。
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
        foreach (var line in info.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
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
                // 等待客户端重连到重启后的 Redis。
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
    /// 删除测试键并收集清理阶段的连接异常。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="key">逻辑缓存键。</param>
    /// <param name="cleanupErrors">清理阶段异常集合。</param>
    private static async Task TryDeleteAsync(IDatabase database, string key,
        ICollection<Exception> cleanupErrors)
    {
        try
        {
            await database.KeyDeleteAsync(key);
        }
        catch (RedisConnectionException exception)
        {
            cleanupErrors.Add(exception);
        }
        catch (RedisTimeoutException exception)
        {
            cleanupErrors.Add(exception);
        }
    }

    /// <summary>
    /// 判断两个端点是否指向同一节点。
    /// </summary>
    /// <param name="left">左侧 Redis 端点。</param>
    /// <param name="right">右侧 Redis 端点。</param>
    /// <returns>如果两个端点指向同一主机和端口，则返回 true。</returns>
    private static bool AreSameEndpoint((string Host, int Port) left,
        (string Host, int Port) right) =>
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
        left.Port == right.Port;

    /// <summary>
    /// 判断主机是否为回环地址。
    /// </summary>
    /// <param name="host">服务主机。</param>
    /// <returns>如果主机是本地回环地址，则返回 true。</returns>
    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 判断容器名称是否符合隔离测试规则。
    /// </summary>
    /// <param name="name">待检查的 Docker 容器名称。</param>
    /// <returns>如果容器名符合隔离测试规则，则返回 true。</returns>
    private static bool IsIsolatedContainerName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.StartsWith("bing-cache-triple-", StringComparison.Ordinal) &&
        name.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '/', '\\' }) < 0;

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
