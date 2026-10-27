using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSRedis;
using Bing.Resilience.Distributed;

namespace Bing.Resilience.Distributed.Redis;

/// <summary>注入宿主管理的客户端；不使用静态 RedisHelper，也不释放宿主连接。</summary>
public sealed class RedisDistributedCircuitStateStore(CSRedisClient client, string keyPrefix = "bing:resilience:circuit:") : IDistributedCircuitStateStore
{
    private static readonly string Script = LoadScript();
    private int _pending;
    private const int MaximumPendingOperations = 128;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static string LoadScript()
    {
        using var stream = typeof(RedisDistributedCircuitStateStore).Assembly.GetManifestResourceStream("Bing.Resilience.Distributed.Redis.CircuitState.lua")
            ?? throw new InvalidOperationException("缺少 Redis 熔断脚本资源。");
        using var reader = new StreamReader(stream, Encoding.UTF8); return reader.ReadToEnd();
    }
    public async ValueTask<DistributedCircuitAdmission> AdmitAsync(string key, DistributedCircuitConfiguration configuration, string attemptId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(attemptId) || attemptId.Length > 128) throw new ResilienceConfigurationException("尝试 ID 无效。");
        var value = await Execute("admit", key, configuration, attemptId, Guid.NewGuid().ToString("N"), "", -1, cancellationToken).ConfigureAwait(false);
        return new(value.Allowed, key, attemptId, value.Epoch, configuration.Version, string.IsNullOrEmpty(value.Probe) ? null : value.Probe,
            value.ProbeUntil > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(value.ProbeUntil) : null,
            TimeSpan.FromMilliseconds(Math.Max(0, Math.Max(value.OpenUntil, value.State == 2 ? value.ProbeUntil : 0) - value.Now)));
    }
    public async ValueTask ReportAsync(DistributedCircuitAdmission admission, DistributedCircuitConfiguration configuration, DistributedCircuitOutcome outcome, CancellationToken cancellationToken = default)
    {
        if (admission.ConfigVersion != configuration.Version) throw new DistributedCircuitVersionException("上报配置与准入版本不一致。");
        if (!admission.Allowed) throw new ResilienceConfigurationException("未准入的尝试不能上报。");
        if (!Enum.IsDefined(typeof(DistributedCircuitOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        await Execute("report", admission.CircuitKey, configuration, admission.AttemptId, admission.ProbeLeaseToken ?? "",
            outcome.ToString().ToLowerInvariant(), admission.Epoch, cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask<DistributedCircuitSnapshot> GetSnapshotAsync(string key, DistributedCircuitConfiguration configuration, CancellationToken cancellationToken = default) =>
        Snapshot(await Execute("read", key, configuration, "", "", "", -1, cancellationToken).ConfigureAwait(false), configuration);
    public async ValueTask<DistributedCircuitSnapshot> ControlAsync(string key, DistributedCircuitConfiguration configuration, long expectedEpoch, bool isolate, CancellationToken cancellationToken = default) =>
        Snapshot(await Execute(isolate ? "isolate" : "close", key, configuration, "", "", "", expectedEpoch, cancellationToken).ConfigureAwait(false), configuration);
    private static DistributedCircuitSnapshot Snapshot(Reply value, DistributedCircuitConfiguration configuration) =>
        new((ResilienceCircuitState)value.State, value.Epoch, configuration.Version, DateTimeOffset.FromUnixTimeMilliseconds(value.Now),
            value.OpenUntil > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(value.OpenUntil) : null,
            value.State == 2 && value.ProbeUntil > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(value.ProbeUntil) : null);
    private async ValueTask<Reply> Execute(string action, string key, DistributedCircuitConfiguration c, string attempt, string token,
        string outcome, long epoch, CancellationToken cancellationToken)
    {
        c.Validate();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 512 || string.IsNullOrWhiteSpace(keyPrefix))
            throw new ResilienceConfigurationException("分布式熔断键无效。");
        cancellationToken.ThrowIfCancellationRequested();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var redisKey = keyPrefix + "{" + hash + "}";
        var config = JsonSerializer.Serialize(c);
        object[] args = { action, config, c.Version, c.FailureRatio.ToString(CultureInfo.InvariantCulture), c.MinimumThroughput,
            c.SamplingMilliseconds, c.BreakMilliseconds, c.ProbeLeaseMilliseconds, attempt, token, outcome, epoch };
        // WaitAsync 只取消调用方等待；脚本可能已执行。AttemptId、epoch 和租约处理这种不确定性。
        // CSRedis 的异步入口也可能在获取/归还连接时同步 Ping。隔离该前缀，保证调用预算可取消；
        // 未完成脚本继续占用有界槽位，不能因大量已取消等待者无限堆积后台工作。
        if (Interlocked.Increment(ref _pending) > MaximumPendingOperations)
        {
            Interlocked.Decrement(ref _pending);
            throw new InvalidOperationException("Redis 待完成操作达到上限。");
        }
        var execution = Task.Run(async () =>
        {
            try { return await client.EvalAsync(Script, redisKey, args).ConfigureAwait(false); }
            finally { Interlocked.Decrement(ref _pending); }
        });
        _ = execution.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var raw = await execution.WaitAsync(cancellationToken).ConfigureAwait(false);
        var reply = JsonSerializer.Deserialize<Reply>(Convert.ToString(raw, CultureInfo.InvariantCulture)!, Json)
            ?? throw new InvalidOperationException("Redis 熔断协议返回空值。");
        if (reply.Error is "configuration_conflict" or "epoch_conflict")
            throw new DistributedCircuitVersionException(reply.Error);
        if (reply.Error is not null) throw new InvalidOperationException("Redis 熔断协议拒绝：" + reply.Error);
        return reply;
    }
    private sealed class Reply
    {
        public bool Allowed { get; set; }
        public int State { get; set; }
        public long Epoch { get; set; }
        public long Now { get; set; }
        public long OpenUntil { get; set; }
        public long ProbeUntil { get; set; }
        public string? Probe { get; set; }
        public string? Error { get; set; }
    }
}
