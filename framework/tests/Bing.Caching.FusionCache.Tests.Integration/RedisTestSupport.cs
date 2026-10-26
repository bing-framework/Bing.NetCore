global using Xunit;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 仅在显式提供可丢弃 Redis 端点时运行的集成测试属性。
/// </summary>
public sealed class RedisFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="RedisFactAttribute" /> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 未启用集成测试或未配置可丢弃 Redis 端点时，将测试标记为跳过。
    /// </remarks>
    public RedisFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") != "true" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_REDIS")))
            Skip = "Set RUN_INTEGRATION_TESTS=true and BING_TEST_FUSIONCACHE_REDIS to run against disposable Redis.";
    }
}

/// <summary>
/// 仅在显式命名本机可丢弃容器时运行的故障注入测试属性。
/// </summary>
public sealed class RedisContainerFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="RedisContainerFactAttribute" /> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 未启用集成测试、容器名称不符合命名约定或端点不是回环地址时，将故障注入测试标记为跳过。
    /// </remarks>
    public RedisContainerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_INTEGRATION_TESTS") != "true" ||
            Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_CONTAINER")?.StartsWith("bing-fusioncache-", StringComparison.Ordinal) != true ||
            Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_REDIS")?.StartsWith("127.0.0.1:", StringComparison.Ordinal) != true)
            Skip = "Fault injection requires an explicitly named bing-fusioncache-* loopback container.";
    }
}

/// <summary>
/// 为真实 Redis 集成测试提供命名空间、轮询和容器控制工具。
/// </summary>
internal static class RedisTestSupport
{
    /// <summary>
    /// 生成隔离本次测试数据的缓存命名空间。
    /// </summary>
    /// <returns>包含随机标识的命名空间字符串。</returns>
    internal static string NewNamespace() => "fc-test:" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// 轮询等待条件成立，超时后抛出取消异常。
    /// </summary>
    /// <param name="condition">返回是否已满足条件的异步判断。</param>
    /// <param name="seconds">最长等待秒数，默认 15 秒。</param>
    internal static async Task Eventually(Func<Task<bool>> condition, int seconds = 15)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        while (!await condition())
            await Task.Delay(30, timeout.Token);
    }

    /// <summary>
    /// 对显式命名的测试容器执行启动或停止命令。
    /// </summary>
    /// <param name="action">传给 <c>docker</c> 的子命令，例如 <c>start</c> 或 <c>stop</c>。</param>
    internal static async Task Docker(string action)
    {
        var container = Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_CONTAINER")!;
        Assert.StartsWith("bing-fusioncache-", container);
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(action);
        start.ArgumentList.Add(container);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}

/// <summary>
/// 承载独立 OS 进程的缓存测试节点。
/// </summary>
/// <remarks>
/// 每个节点通过 JSON 行协议执行真实缓存操作，用于验证跨进程一致性和进程崩溃后的恢复边界。
/// </remarks>
internal sealed class CacheNode : IAsyncDisposable
{
    /// <summary>
    /// 运行探测程序的子进程。
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// 子进程标准错误输出的异步读取任务。
    /// </summary>
    private readonly Task<string> _errors;

    /// <summary>
    /// 初始化一个 <see cref="CacheNode" /> 类的新实例。
    /// </summary>
    /// <param name="process">已启动的探测子进程。</param>
    private CacheNode(Process process)
    {
        _process = process;
        _errors = process.StandardError.ReadToEndAsync();
    }

    /// <summary>
    /// 启动新的探测进程并等待其报告就绪。
    /// </summary>
    /// <param name="cacheNamespace">探测进程使用的缓存命名空间。</param>
    /// <param name="distributedLock">是否启用 Redis 分布式锁。</param>
    /// <param name="crashWindow">是否模拟崩溃窗口并延迟自动恢复。</param>
    /// <returns>已就绪的缓存测试节点。</returns>
    /// <exception cref="InvalidOperationException">无法定位仓库根目录时抛出。</exception>
    internal static async Task<CacheNode> Start(string cacheNamespace, bool distributedLock = false, bool crashWindow = false)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Bing.All.slnx")))
            directory = directory.Parent;
        if (directory == null) throw new InvalidOperationException("Cannot find the repository root.");
#if NET10_0
        const string tfm = "net10.0";
#else
        const string tfm = "net8.0";
#endif
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var probe = Path.Combine(directory.FullName, "framework", "tests", "Bing.Caching.FusionCache.Probe",
            "bin", configuration, tfm, "Bing.Caching.FusionCache.Probe.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true
        };
        start.ArgumentList.Add(probe);
        start.ArgumentList.Add(cacheNamespace);
        if (distributedLock) start.ArgumentList.Add("--lock");
        if (crashWindow) start.ArgumentList.Add("--crash-window");
        var node = new CacheNode(Process.Start(start)!);
        try
        {
            var ready = await node._process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.NotNull(ready);
            Assert.True(JsonDocument.Parse(ready!).RootElement.GetProperty("Ready").GetBoolean());
            return node;
        }
        catch { await node.DisposeAsync(); throw; }
    }

    /// <summary>
    /// 向探测进程发送命令并等待单行响应。
    /// </summary>
    /// <param name="command">要执行的缓存探测命令。</param>
    /// <returns>探测进程返回的命令结果。</returns>
    internal async Task<ProbeResponse> Send(ProbeCommand command)
    {
        await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(command));
        await _process.StandardInput.FlushAsync();
        var line = await _process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.NotNull(line);
        var response = JsonSerializer.Deserialize<ProbeResponse>(line!)!;
        Assert.Null(response.Error);
        return response;
    }

    /// <summary>
    /// 强制终止探测进程树，并确认进程以非零代码退出。
    /// </summary>
    internal async Task CrashAsync()
    {
        _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(0, _process.ExitCode);
    }

    /// <summary>
    /// 发送退出命令并等待探测进程结束，必要时强制终止。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new ProbeCommand("exit")));
            await _process.StandardInput.FlushAsync();
            try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
        }
        var errors = await _errors;
        _process.Dispose();
        Assert.True(string.IsNullOrWhiteSpace(errors), errors);
    }
}
