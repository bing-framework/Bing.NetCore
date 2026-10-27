using Bing.Resilience;

/// <summary>能力1/4：BuiltIn 通用管线和缓存句柄的运行中切换。</summary>
internal static class CoreDemo
{
    public static async Task Run()
    {
        var services = new ServiceCollection();
        services.AddBingResilience().AddBuiltInProvider("blue").AddBuiltInProvider("green")
            .UseDefaultProvider("blue").AddRuntimeSwitching();
        services.AddBingResiliencePipeline<int>("catalog", o =>
        {
            o.ExecutionTimeout = TimeSpan.FromSeconds(2);
            o.Retry.MaxRetryAttempts = 1; o.Retry.Delay = TimeSpan.Zero;
            o.Retry.ShouldHandle = args => args.Outcome.Exception is IOException;
            o.Concurrency.Enabled = true; o.Concurrency.PermitLimit = 4;
            o.RateLimit.Enabled = true; o.RateLimit.TokenLimit = 10; o.RateLimit.TokensPerPeriod = 10;
            o.RateLimit.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
        });
        await using var host = services.BuildServiceProvider();
        var pipe = host.GetRequiredService<IResiliencePipelineProvider>().GetPipeline<int>("catalog");
        var attempts = 0;
        var value = await pipe.ExecuteAsync(_ => ++attempts == 1 ? throw new IOException("可恢复故障") : ValueTask.FromResult(42));
        var runtime = host.GetRequiredService<IResilienceRuntimeManager>();
        var before = runtime.GetSnapshot("generic:catalog");
        var switched = await runtime.SwitchAsync("generic:catalog", "green", before.Version);
        if (value != 42 || attempts != 2 || await pipe.ExecuteAsync(_ => ValueTask.FromResult(43)) != 43) throw new InvalidOperationException();
        Console.WriteLine($"BuiltIn 重试={attempts}，稳定句柄切换到代 {switched.Snapshot.Version}，配额继续共享。");
        // 切到不同引擎时，安装可选包并登记 .AddFencesProvider("green")；调用端不需要更改。
    }
}
