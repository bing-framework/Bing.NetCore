using System.Text;
using System.Text.Json;
using Bing.Resilience;
using Bing.Resilience.Distributed;
using Bing.Resilience.Distributed.Redis;
using CSRedis;

/// <summary>能力5：显式选择失联策略，连接由宿主管理；仅在 --redis 时访问指定测试服务。</summary>
internal static class DistributedDemo
{
    public static async Task Run()
    {
        var file = Environment.GetEnvironmentVariable("BING_RESILIENCE_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("请设置任务专属测试连接文件。");
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(file, Encoding.UTF8));
        using var client = new CSRedisClient(config.RootElement.GetProperty("redis").GetProperty("connectionString").GetString()!);
        var store = new RedisDistributedCircuitStateStore(client);
        var settings = new DistributedCircuitConfiguration("1", .5, 2);
        var key = "sample:" + Guid.NewGuid().ToString("N");
        var observer = new DistributedCircuitObserver<int>(store, new()
        { CircuitKey = key, Configuration = settings, FailureMode = DistributedCircuitFailureMode.FailClosed },
            outcome => outcome.Exception is IOException);
        var services = new ServiceCollection();
        services.AddBingResilience().AddBuiltInProvider().UseDefaultProvider("builtin");
        services.AddBingResiliencePipeline<int>("distributed", o => o.AttemptObservers.Add(observer));
        await using var host = services.BuildServiceProvider();
        var pipe = host.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("distributed");
        for (var i = 0; i < 2; i++) try { await pipe.ExecuteAsync(_ => throw new IOException("远端故障")); } catch (IOException) { }
        var state = await store.GetSnapshotAsync(key, settings);
        if (state.State != ResilienceCircuitState.Open) throw new InvalidOperationException();
        Console.WriteLine($"Redis 共享状态={state.State}，epoch={state.Epoch}");
    }
}
