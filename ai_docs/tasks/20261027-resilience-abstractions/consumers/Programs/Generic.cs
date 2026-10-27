using Bing.Resilience;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
var services = new ServiceCollection();
#if POLLY
services.AddBingResilience().AddPollyProvider("primary").UseDefaultProvider("primary");
#else
services.AddBingResilience().AddFencesProvider("primary").UseDefaultProvider("primary");
#endif
services.AddBingResiliencePipeline<string>("catalog", options =>
{
    options.ExecutionTimeout = TimeSpan.FromSeconds(2);
    options.AttemptTimeout = TimeSpan.FromSeconds(1);
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.Zero;
    options.Retry.ShouldHandle = a => a.Outcome.Exception is IOException;
    options.Concurrency.Enabled = true;
    options.RateLimit.Enabled = true;
    options.RateLimit.TokenLimit = 8;
    options.RateLimit.TokensPerPeriod = 8;
    options.RateLimit.ReplenishmentPeriod = TimeSpan.FromSeconds(1);
    options.CircuitBreaker.Enabled = true;
    options.CircuitBreaker.EnableManualControl = true;
    options.CircuitBreaker.ShouldHandle = a => a.Outcome.Exception is IOException;
    options.Fallback.Enabled = true;
    options.Fallback.ShouldHandle = a => a.Outcome.Exception is IOException or ResilienceCircuitOpenException;
    options.Fallback.Action = _ => new("cached-catalog");
});
services.AddBingResiliencePipeline<string>("parallel", options =>
{
    options.Hedging.Enabled = true;
    options.Hedging.AllowParallelExecution = true;
    options.Hedging.Delay = TimeSpan.FromMilliseconds(20);
    options.Hedging.ShouldHandle = a => a.Outcome.Exception is IOException;
});
using var provider = services.BuildServiceProvider();
var pipelines = provider.GetRequiredService<IResiliencePipelineProvider>();
pipelines.Validate();
var catalog = pipelines.GetPipeline<string>("catalog");
var attempts = 0;
var result = await catalog.ExecuteAsync(_ =>
{
    if (++attempts == 1) throw new IOException("模拟瞬态故障");
    return new ValueTask<string>("catalog-ok");
});
if (result != "catalog-ok" || attempts != 2) throw new Exception("重试验证失败");
var control = provider.GetRequiredService<IResilienceCircuitController>();
var identity = new ResiliencePipelineIdentity("generic", "catalog", typeof(string));
await control.IsolateAsync(identity);
if (await catalog.ExecuteAsync(_ => new ValueTask<string>("unexpected")) != "cached-catalog")
    throw new Exception("隔离降级验证失败");
await control.CloseAsync(identity);
var parallel = await pipelines.GetPipeline<string>("parallel").ExecuteAsync(async (context, token) =>
{
    if (context.AttemptNumber == 0) await Task.Delay(1000, token);
    return "branch-" + context.AttemptNumber;
}, new ResilienceExecutionContext());
if (parallel != "branch-1") throw new Exception("Hedging 验证失败");
Console.WriteLine($"Generic consumer PASS: {Environment.Version}; attempts={attempts}; fallback=cached-catalog; hedge={parallel}");
