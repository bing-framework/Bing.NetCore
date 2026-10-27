using Bing.Resilience.Operations;

namespace Bing.Resilience.Replay;

public enum ReplayOutcome { Succeeded, FailedNoEffect, RetryableNoEffect, Unknown }
public sealed record ReplayHandlerResult(ReplayOutcome Outcome, string? Result = null, string Reason = "", TimeSpan? RetryAfter = null);
/// <summary>持久化命令仅使用登记的名称和版本，不反射加载任意类型。OperationId 由调用方在提交前固定。</summary>
public sealed record ReplayCommand(string OperationId, string OperationName, string ContractVersion, string CommandType, string SchemaVersion,
    string AccessScope, string IdempotencyKey, string Payload, DateTimeOffset Deadline);
public sealed record ReplayExecutionContext(ReplayCommand Command, int Attempt, bool IsConfirmation)
{
    /// <summary>供 HTTP 桥接禁止内层重放；每个处理器只执行一次远端动作。</summary>
    public bool DurableAttempt => true;
}
public interface IReplayCommandHandler
{
    ValueTask<ReplayHandlerResult> ExecuteAsync(ReplayExecutionContext context, CancellationToken token);
    ValueTask<ReplayHandlerResult> ConfirmAsync(ReplayExecutionContext context, CancellationToken token);
}
public sealed record ReplayHandlerRegistration(string Name, string SchemaVersion, string OperationName,
    Func<IServiceProvider, IReplayCommandHandler> Factory, bool AllowSameKeyRecovery = false);
public sealed class ReplayOptions
{
    public int MaxAttempts { get; set; } = 10;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumLifetime { get; set; } = TimeSpan.FromDays(1);
    public bool StartWorker { get; set; }
    /// <summary>默认不清理；显式登记后可通过 Worker.CleanupOnceAsync 或后台循环清理终态载荷。</summary>
    public OperationRetentionPolicy? Retention { get; set; }
    /// <summary>入队及每次认领均重验业务授权。访问令牌由处理器即时获取，不持久化。</summary>
    public Func<ReplayCommand, CancellationToken, ValueTask<bool>>? Authorize { get; set; }
}
public interface IReplayCoordinator
{
    ValueTask<DurableOperationReceipt> EnqueueAsync(ReplayCommand command, CancellationToken token = default);
    ValueTask<OperationRecord> EnqueueInTransactionAsync(IOperationTransaction transaction, ReplayCommand command, CancellationToken token = default);
    ValueTask<OperationRecord?> GetAsync(string operationId, string accessScope, CancellationToken token = default);
    ValueTask CancelAsync(string operationId, string accessScope, long expectedVersion, string actor, string reason, CancellationToken token = default);
}
internal sealed record ReplayDocument(int Format, ReplayCommand Command, string? Result = null, string? LastReason = null);
