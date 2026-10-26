using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Bing.Tests.Modularity;

/// <summary>
/// 失败时通过显式门控发布 live-process 采证握手。
/// </summary>
/// <remarks>
/// 默认关闭；只在测试已经得到非空首快照后调用。诊断数据只含字符串和值快照，
/// 不持有 provider/module/recorder 的强引用，也不把 target 放进异常或日志。
/// </remarks>
internal static class GcCaptureHandshake
{
    /// <summary>
    /// 控制是否启用失败采证的门控环境变量名称。
    /// </summary>
    private const string GateVariable = "BING_GC_CAPTURE";

    /// <summary>
    /// 标识本次采证运行的运行 ID 环境变量名称。
    /// </summary>
    private const string RunIdVariable = "BING_GC_CAPTURE_RUN_ID";

    /// <summary>
    /// 用于匹配 ready 与 ack 的随机串环境变量名称。
    /// </summary>
    private const string NonceVariable = "BING_GC_CAPTURE_NONCE";

    /// <summary>
    /// 采证输出根目录的环境变量名称。
    /// </summary>
    private const string DirectoryVariable = "BING_GC_CAPTURE_DIR";

    /// <summary>
    /// 标识候选实现的环境变量名称。
    /// </summary>
    private const string CandidateVariable = "BING_GC_CAPTURE_CANDIDATE";

    /// <summary>
    /// 等待外部 ack 的最长时间。
    /// </summary>
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 获取失败采证门控是否启用。
    /// </summary>
    internal static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(GateVariable), "1", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 在首快照非空时发布 ready，并等待同 nonce 的 ack；异常/超时由调用方继续抛原失败。
    /// </summary>
    /// <param name="testName">产生失败采证的测试名称。</param>
    /// <param name="stage">当前采证阶段标识。</param>
    /// <param name="entries">只含值元数据的存活对象描述。</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Publish(string testName, string stage, IReadOnlyList<GcCaptureEntry> entries)
    {
        if (!IsEnabled || entries == null || entries.Count == 0)
            return;
        try { PublishCore(testName, stage, entries); }
        catch (Exception exception) { TryWriteError(exception); }
    }

    /// <summary>
    /// 发布 ready 快照并通过原子文件操作等待外部确认。
    /// </summary>
    /// <param name="testName">产生失败采证的测试名称。</param>
    /// <param name="stage">当前采证阶段标识。</param>
    /// <param name="entries">只含值元数据的存活对象描述。</param>
    private static void PublishCore(string testName, string stage, IReadOnlyList<GcCaptureEntry> entries)
    {
        var runId = Required(RunIdVariable);
        var nonce = Required(NonceVariable);
        var root = Required(DirectoryVariable);
        var candidate = Environment.GetEnvironmentVariable(CandidateVariable) ?? "UNSPECIFIED";
        var captureDirectory = Path.Combine(root, runId);
        Directory.CreateDirectory(captureDirectory);

        var claimPath = Path.Combine(captureDirectory, "claim.json");
        try
        {
            using var claim = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough);
            var claimBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                runId,
                nonce,
                processId = Environment.ProcessId,
                claimedAtUtc = DateTimeOffset.UtcNow.ToString("o")
            }));
            claim.Write(claimBytes, 0, claimBytes.Length);
            claim.Flush(true);
        }
        catch (IOException)
        {
            return; // 同一 runId 的首个失败已经 claim。
        }

        var process = Process.GetCurrentProcess();
        var ready = new
        {
            schema = "bing-gc-capture-ready-v1",
            runId,
            nonce,
            candidate,
            testName,
            stage,
            processId = Environment.ProcessId,
            processStartTimeUtc = process.StartTime.ToUniversalTime().ToString("o"),
            processStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
            targetFramework = AppContext.TargetFrameworkName,
            capturedAtUtc = DateTimeOffset.UtcNow.ToString("o"),
            entries
        };
        var readyPath = Path.Combine(captureDirectory, "ready.json");
        WriteAtomic(readyPath, JsonSerializer.Serialize(ready));

        var ackPath = Path.Combine(captureDirectory, "ack.json");
        var deadline = DateTime.UtcNow.Add(AckTimeout);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(ackPath))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(ackPath, Encoding.UTF8));
                    if (document.RootElement.TryGetProperty("nonce", out var ackNonce) &&
                        string.Equals(ackNonce.GetString(), nonce, StringComparison.Ordinal))
                        return;
                }
                catch (Exception) { }
            }
            Thread.Sleep(100);
        }
        TryWriteError(new TimeoutException($"ack timeout for runId {runId}"));
    }

    /// <summary>
    /// 读取必需的环境变量值。
    /// </summary>
    /// <param name="name">环境变量名称。</param>
    /// <returns>非空的环境变量值。</returns>
    /// <exception cref="InvalidOperationException">环境变量缺失或为空时抛出。</exception>
    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"缺少环境变量 {name}。");

    /// <summary>
    /// 先写入同目录临时文件再原子移动到目标路径，避免读取方看到半成品。
    /// </summary>
    /// <param name="path">最终输出路径。</param>
    /// <param name="content">要写入的 UTF-8 文本。</param>
    private static void WriteAtomic(string path, string content)
    {
        var temp = path + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   4096, FileOptions.WriteThrough))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
        File.Move(temp, path, false);
    }

    /// <summary>
    /// 尽力记录采证钩子自身异常，不覆盖测试原始失败。
    /// </summary>
    /// <param name="exception">采证钩子抛出的异常。</param>
    private static void TryWriteError(Exception exception)
    {
        try
        {
            var root = Environment.GetEnvironmentVariable(DirectoryVariable);
            var runId = Environment.GetEnvironmentVariable(RunIdVariable);
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(runId))
                return;
            var path = Path.Combine(root, runId, "hook-error.txt");
            WriteAtomic(path, exception.ToString());
        }
        catch (Exception) { }
    }
}

/// <summary>
/// 首快照中的单个存活对象值元数据。
/// </summary>
/// <remarks>
/// 不包含 target 强引用，避免采证本身延长被测对象生命周期。
/// </remarks>
internal sealed class GcCaptureEntry
{
    /// <summary>
    /// 初始化一个 <see cref="GcCaptureEntry" /> 类的新实例。
    /// </summary>
    /// <param name="appId">产生该对象的应用序号。</param>
    /// <param name="role">对象在应用中的角色标签。</param>
    /// <param name="typeName">对象的完整类型名称。</param>
    public GcCaptureEntry(int appId, string role, string typeName)
    {
        AppId = appId;
        Role = role;
        TypeName = typeName;
    }

    /// <summary>
    /// 获取产生该对象的应用序号。
    /// </summary>
    public int AppId { get; }

    /// <summary>
    /// 获取对象在应用中的角色标签。
    /// </summary>
    public string Role { get; }

    /// <summary>
    /// 获取对象的完整类型名称。
    /// </summary>
    public string TypeName { get; }
}
