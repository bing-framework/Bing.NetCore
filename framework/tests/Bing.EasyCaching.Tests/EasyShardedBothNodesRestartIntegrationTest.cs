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
/// 客户端分片两个 Redis 节点同时重启测试门控。
/// </summary>
public sealed class EasyShardedBothNodesRestartFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedBothNodesRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedBothNodesRestartFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_MULTI_SHARD_FAILURE_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要 RUN_INTEGRATION_TESTS=true、RUN_CACHE_MULTI_SHARD_FAILURE_TESTS=true、两个独立 Redis 端点，并显式设置 BING_TEST_ALLOW_REDIS_RESTART=true。";
            return;
        }

        var first = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var second = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        if (!IsIsolatedContainerName(first) || !IsIsolatedContainerName(second) ||
            string.Equals(first, second, StringComparison.Ordinal))
        {
            Skip = "需要 BING_TEST_RESTART_CONTAINER 和 BING_TEST_RESTART_CONTAINER_2 指向两个不同的 bing-cache-both- 开头临时容器。";
        }
    }

    /// <summary>
    /// 判断容器名称是否符合隔离测试规则。
    /// </summary>
    /// <param name="name">待验证的 Docker 容器名。</param>
    /// <returns>如果容器名符合隔离测试规则，则返回 true。</returns>
    private static bool IsIsolatedContainerName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.StartsWith("bing-cache-both-", StringComparison.Ordinal) &&
        name.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '/', '\\' }) < 0;
}

/// <summary>
/// 客户端分片单节点连续重启测试门控。
/// </summary>
public sealed class EasyShardedConsecutiveRestartFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="EasyShardedConsecutiveRestartFactAttribute"/> 类型的实例。
    /// </summary>
    public EasyShardedConsecutiveRestartFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS"), "true",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUN_CACHE_CONSECUTIVE_SHARD_FAILURE_TESTS"),
                "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_EASYCACHING_REDIS")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_REDIS_SHARD_CONNECTION")) ||
            !string.Equals(Environment.GetEnvironmentVariable("BING_TEST_ALLOW_REDIS_RESTART"), "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "需要 RUN_INTEGRATION_TESTS=true、RUN_CACHE_CONSECUTIVE_SHARD_FAILURE_TESTS=true、两个独立 Redis 端点，并显式设置 BING_TEST_ALLOW_REDIS_RESTART=true。";
            return;
        }

        var first = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var second = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        if (!IsIsolatedContainerName(first) || !IsIsolatedContainerName(second) ||
            string.Equals(first, second, StringComparison.Ordinal))
        {
            Skip = "需要 BING_TEST_RESTART_CONTAINER 和 BING_TEST_RESTART_CONTAINER_2 指向两个不同的 bing-cache-both- 开头临时容器。";
        }
    }

    /// <summary>
    /// 判断容器名称是否符合隔离测试规则。
    /// </summary>
    /// <param name="name">待验证的 Docker 容器名。</param>
    /// <returns>如果容器名符合隔离测试规则，则返回 true。</returns>
    private static bool IsIsolatedContainerName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.StartsWith("bing-cache-both-", StringComparison.Ordinal) &&
        name.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '/', '\\' }) < 0;
}

/// <summary>
/// 验证两个客户端分片同时重启时旧管理器显式失败，重建后两个分片均可恢复。
/// </summary>
public sealed class EasyShardedBothNodesRestartIntegrationTest
{
    /// <summary>
    /// 两个客户端分片同时重启时旧 Manager 失败，重建后两个分片均恢复访问。
    /// </summary>
    [EasyShardedBothNodesRestartFact]
    public async Task ShardedRedis_BothNodesRestartTogether_OldManagerFailsAndRebuiltManagerRecovers()
    {
        var firstConnection = FirstConnection;
        var secondConnection = SecondConnection;
        var firstContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var secondContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        var firstEndpoint = ParseEndpoint(firstConnection);
        var secondEndpoint = ParseEndpoint(secondConnection);

        Assert.NotEqual(firstEndpoint, secondEndpoint);
        await ValidateIsolatedContainerAsync(firstContainer, firstEndpoint);
        await ValidateIsolatedContainerAsync(secondContainer, secondEndpoint);

        var configuration = new CacheConfiguration(
            "easy-sharded-both-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-both-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var oldProvider = BuildCache(configuration, firstConnection, secondConnection);
        using var firstControl = ConnectForControl(firstConnection);
        using var secondControl = ConnectForControl(secondConnection);
        var firstServer = firstControl.GetServer(firstEndpoint.Host, firstEndpoint.Port);
        var secondServer = secondControl.GetServer(secondEndpoint.Host, secondEndpoint.Port);
        var firstDatabase = firstControl.GetDatabase();
        var secondDatabase = secondControl.GetDatabase();
        var cache = oldProvider.GetRequiredService<IRedisCache>();
        var firstKey = FindKeyForShard(configuration, 0);
        var secondKey = FindKeyForShard(configuration, 1);
        var firstPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, firstKey);
        var secondPhysicalKey = CacheKeyFormat.GetPhysicalKey(configuration, secondKey);

        try
        {
            await cache.SetAsync(firstKey, 101);
            await cache.SetAsync(secondKey, 202);
            Assert.Equal(101, await cache.GetAsync<int>(firstKey));
            Assert.Equal(202, await cache.GetAsync<int>(secondKey));

            var oldRunIds = await Task.WhenAll(ReadRunIdAsync(firstServer),
                ReadRunIdAsync(secondServer));

            // 两个 restart 进程在同一批次启动，验证两个分片同时发生故障。
            await Task.WhenAll(
                RunDockerAsync("restart", "--time", "1", firstContainer),
                RunDockerAsync("restart", "--time", "1", secondContainer));

            var newRunIds = await Task.WhenAll(
                WaitForRunIdChangeAsync(firstServer, oldRunIds[0], TimeSpan.FromSeconds(30)),
                WaitForRunIdChangeAsync(secondServer, oldRunIds[1], TimeSpan.FromSeconds(30)));
            Assert.NotEqual(oldRunIds[0], newRunIds[0]);
            Assert.NotEqual(oldRunIds[1], newRunIds[1]);

            var firstError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(firstKey), TimeSpan.FromSeconds(30));
            var secondError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(secondKey), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", firstError.Message, StringComparison.Ordinal);
            Assert.Contains("身份", secondError.Message, StringComparison.Ordinal);

            var batchError = await WaitForIdentityErrorAsync(
                () => cache.GetAllAsync<int>(new[] { firstKey, secondKey }),
                TimeSpan.FromSeconds(30));
            Assert.Contains("身份", batchError.Message, StringComparison.Ordinal);

            using var rebuiltProvider = BuildCache(configuration, firstConnection, secondConnection);
            var rebuiltCache = rebuiltProvider.GetRequiredService<IRedisCache>();
            await rebuiltCache.SetAsync(firstKey, 303);
            await rebuiltCache.SetAsync(secondKey, 404);
            Assert.Equal(303, await rebuiltCache.GetAsync<int>(firstKey));
            Assert.Equal(404, await rebuiltCache.GetAsync<int>(secondKey));
        }
        finally
        {
            await TryDeleteAsync(firstDatabase, firstPhysicalKey);
            await TryDeleteAsync(firstDatabase, firstPhysicalKey + ":lease");
            await TryDeleteAsync(secondDatabase, secondPhysicalKey);
            await TryDeleteAsync(secondDatabase, secondPhysicalKey + ":lease");
        }
    }

    /// <summary>
    /// 每次单节点连续重启都使旧 Manager 失败，并在下一次重建前完成恢复。
    /// </summary>
    [EasyShardedConsecutiveRestartFact]
    public async Task ShardedRedis_SameNodeRestartsTwice_EachOldManagerFailsBeforeNextRebuild()
    {
        var firstConnection = FirstConnection;
        var secondConnection = SecondConnection;
        var firstContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER");
        var secondContainer = Environment.GetEnvironmentVariable("BING_TEST_RESTART_CONTAINER_2");
        var firstEndpoint = ParseEndpoint(firstConnection);
        var secondEndpoint = ParseEndpoint(secondConnection);

        Assert.NotEqual(firstEndpoint, secondEndpoint);
        await ValidateIsolatedContainerAsync(firstContainer, firstEndpoint);
        await ValidateIsolatedContainerAsync(secondContainer, secondEndpoint);

        var configuration = new CacheConfiguration(
            "easy-sharded-consecutive-restart-" + Guid.NewGuid().ToString("N"),
            "easy-sharded-consecutive-restart-" + Guid.NewGuid().ToString("N"),
            batchSize: 2,
            maxConcurrency: 2,
            topology: CacheTopology.ClientSharded,
            expectedShardCount: 2);
        using var firstProvider = BuildCache(configuration, firstConnection, secondConnection);
        using var firstControl = ConnectForControl(firstConnection);
        using var secondControl = ConnectForControl(secondConnection);
        var firstServer = firstControl.GetServer(firstEndpoint.Host, firstEndpoint.Port);
        var firstDatabase = firstControl.GetDatabase();
        var cache = firstProvider.GetRequiredService<IRedisCache>();
        var key = FindKeyForShard(configuration, 0);
        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, key);

        try
        {
            await cache.SetAsync(key, 101);
            Assert.Equal(101, await cache.GetAsync<int>(key));

            var firstOldRunId = await ReadRunIdAsync(firstServer);
            await RunDockerAsync("restart", "--time", "1", firstContainer);
            var firstNewRunId = await WaitForRunIdChangeAsync(firstServer, firstOldRunId,
                TimeSpan.FromSeconds(30));
            Assert.NotEqual(firstOldRunId, firstNewRunId);

            var firstManagerError = await WaitForIdentityErrorAsync(
                () => cache.GetAsync<int>(key), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", firstManagerError.Message, StringComparison.Ordinal);

            using var secondProvider = BuildCache(configuration, firstConnection, secondConnection);
            var secondCache = secondProvider.GetRequiredService<IRedisCache>();
            await secondCache.SetAsync(key, 202);
            Assert.Equal(202, await secondCache.GetAsync<int>(key));

            var secondOldRunId = await ReadRunIdAsync(firstServer);
            await RunDockerAsync("restart", "--time", "1", firstContainer);
            var secondNewRunId = await WaitForRunIdChangeAsync(firstServer, secondOldRunId,
                TimeSpan.FromSeconds(30));
            Assert.NotEqual(secondOldRunId, secondNewRunId);

            var secondManagerError = await WaitForIdentityErrorAsync(
                () => secondCache.GetAsync<int>(key), TimeSpan.FromSeconds(30));
            Assert.Contains("身份", secondManagerError.Message, StringComparison.Ordinal);

            using var finalProvider = BuildCache(configuration, firstConnection, secondConnection);
            var finalCache = finalProvider.GetRequiredService<IRedisCache>();
            await finalCache.SetAsync(key, 303);
            Assert.Equal(303, await finalCache.GetAsync<int>(key));
        }
        finally
        {
            await TryDeleteAsync(firstDatabase, physicalKey);
            await TryDeleteAsync(firstDatabase, physicalKey + ":lease");
        }
    }

    /// <summary>
    /// 创建绑定两个 Redis 分片的客户端分片服务容器。
    /// </summary>
    /// <param name="configuration">缓存配置。</param>
    /// <param name="firstConnection">Redis 连接。</param>
    /// <param name="secondConnection">Redis 连接。</param>
    /// <returns>返回已注册客户端分片 Redis Provider 的服务容器。</returns>
    private static ServiceProvider BuildCache(CacheConfiguration configuration,
        string firstConnection, string secondConnection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShardedRedisCaching(configuration, new[]
        {
            ConfigureNode(firstConnection),
            ConfigureNode(secondConnection)
        });
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
            var key = "both-restart:" + index;
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
    /// 验证容器处于隔离测试环境。
    /// </summary>
    /// <param name="container">测试容器。</param>
    /// <param name="endpoint">Redis 端点。</param>
    private static async Task ValidateIsolatedContainerAsync(string container,
        (string Host, int Port) endpoint)
    {
        var inspect = (await RunDockerAsync("inspect", "--format", "{{.Name}}|{{.State.Running}}",
            container)).Trim();
        Assert.Equal("/" + container + "|true", inspect);
        var publishedPort = (await RunDockerAsync("port", container, "6379/tcp")).Trim();
        Assert.Equal($"127.0.0.1:{endpoint.Port}", publishedPort);
        Assert.Contains(endpoint.Host, new[] { "127.0.0.1", "localhost" });
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
    /// 等待 Redis 节点运行标识变为不同于旧值的值。
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
    /// 删除测试键，并忽略清理阶段的短暂连接失败。
    /// </summary>
    /// <param name="database">Redis 数据库。</param>
    /// <param name="key">逻辑缓存键。</param>
    private static async Task TryDeleteAsync(IDatabase database, string key)
    {
        try
        {
            await database.KeyDeleteAsync(key);
        }
        catch (RedisConnectionException)
        {
            // 节点重启失败时清理可能无法完成，但不能覆盖主断言。
        }
        catch (RedisTimeoutException)
        {
            // 节点重启失败时清理可能超时，但不能覆盖主断言。
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
            throw new InvalidOperationException("缺少 Redis 分片连接配置。");
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
}
