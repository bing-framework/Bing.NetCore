using System.Text;
using System.Text.Json;
using Bing.Caching;
using Bing.Caching.FusionCache.Probe;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

if (args.Length == 0) throw new ArgumentException("Provide an isolated test namespace.");
var cacheNamespace = args[0];
using var services = RedisTestHost.Create(cacheNamespace, distributedLock: args.Contains("--lock"),
    rethrow: !args.Contains("--crash-window"), configure: o =>
    {
        if (args.Contains("--crash-window")) o.AutoRecoveryDelay = TimeSpan.FromMinutes(1);
    });
var cache = services.GetRequiredService<IHybridCache>();
var database = services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
Console.WriteLine(JsonSerializer.Serialize(new { Ready = true, Pid = Environment.ProcessId }));
while (await Console.In.ReadLineAsync() is { } line)
{
    var command = JsonSerializer.Deserialize<ProbeCommand>(line)!;
    try
    {
        if (command.Operation == "exit") break;
        int? value = null;
        var hit = false;
        switch (command.Operation)
        {
            case "set":
                await cache.SetAsync<int?>(command.Key, command.Value, tags: command.Tag == null ? null : [command.Tag]);
                break;
            case "get":
                var result = await cache.TryGetAsync<int?>(command.Key);
                value = result.Value;
                hit = result.Hit;
                break;
            case "create":
                value = await cache.GetOrCreateAsync<int?>(command.Key, async token =>
                {
                    var calls = await database.StringIncrementAsync(RedisTestHost.CounterKey(cacheNamespace, command.Key));
                    await Task.Delay(command.DelayMs, token);
                    return (int)calls;
                });
                hit = true;
                break;
            case "remove": await cache.RemoveAsync(command.Key); break;
            case "expire": await cache.ExpireAsync(command.Key); break;
            case "tag": await cache.InvalidateTagAsync(command.Tag!); break;
            case "clear": await cache.ClearAsync(); break;
            default: throw new ArgumentException("Unknown operation.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new ProbeResponse(hit, value, null)));
    }
    catch (Exception error)
    {
        Console.WriteLine(JsonSerializer.Serialize(new ProbeResponse(false, null, error.ToString())));
    }
}

/// <summary>
/// 真实 Redis 集成测试通过标准输入发送的缓存探测命令。
/// </summary>
/// <param name="Operation">要执行的缓存操作名称。</param>
/// <param name="Key">目标业务缓存键，默认 <c>key</c>。</param>
/// <param name="Value">写入操作使用的可选值。</param>
/// <param name="Tag">标签写入或失效操作使用的可选标签。</param>
/// <param name="DelayMs">回源工厂的模拟延迟，单位为毫秒。</param>
public sealed record ProbeCommand(string Operation, string Key = "key", int? Value = null, string? Tag = null, int DelayMs = 200);

/// <summary>
/// 探测进程通过标准输出返回的单行缓存操作结果。
/// </summary>
/// <param name="Hit">读取操作是否命中缓存。</param>
/// <param name="Value">读取或回源得到的可选整数结果。</param>
/// <param name="Error">操作失败时的完整异常文本；成功时为 <see langword="null" />。</param>
public sealed record ProbeResponse(bool Hit, int? Value, string? Error);
