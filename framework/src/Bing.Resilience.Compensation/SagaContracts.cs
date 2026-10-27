using Bing.Resilience.Operations;
namespace Bing.Resilience.Compensation;

public enum SagaOutcome { Succeeded, FailedNoEffect, RetryableNoEffect, Unknown, PartialEffect }
public enum SagaStepAction { Forward, ConfirmForward, Compensate, ConfirmCompensation }
public sealed record SagaStepResult(SagaOutcome Outcome, string? Output = null, string Reason = "", TimeSpan? RetryAfter = null);
public sealed record SagaStepDefinition(string Id, string Handler, string HandlerVersion, string Input, bool Optional = false,
    bool CanCompensate = true, bool AllowSameKeyRecovery = false, IReadOnlyList<string>? CompensateAfter = null);
public sealed record SagaCommand(string SagaId, string OperationName, string ContractVersion, string AccessScope,
    string IdempotencyKey, IReadOnlyList<SagaStepDefinition> Steps, DateTimeOffset Deadline, bool UseCompensationDag = false);
public sealed record SagaStepContext(SagaCommand Saga, SagaStepDefinition Step, string ForwardKey, string CompensationKey,
    string? Output, int Attempt, SagaStepAction Action, IOperationTransaction? Transaction);
public interface ISagaStepHandler
{
    ValueTask<SagaStepResult> ForwardAsync(SagaStepContext context, CancellationToken token);
    ValueTask<SagaStepResult> ConfirmAsync(SagaStepContext context, CancellationToken token);
    ValueTask<SagaStepResult> CompensateAsync(SagaStepContext context, CancellationToken token);
}
public sealed record SagaHandlerRegistration(string Name, string Version, Func<IServiceProvider, ISagaStepHandler> Factory, bool UsesLocalTransaction = false);
public sealed class SagaOptions
{
    public int MaxAttemptsPerStep { get; set; } = 10;
    public int MaxParallelCompensations { get; set; } = 4;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaximumLifetime { get; set; } = TimeSpan.FromDays(1);
    public bool StartWorker { get; set; }
    /// <summary>默认不清理；显式登记后可通过 Worker.CleanupOnceAsync 或后台循环清理终态载荷。</summary>
    public OperationRetentionPolicy? Retention { get; set; }
    public Func<SagaCommand, CancellationToken, ValueTask<bool>>? Authorize { get; set; }
}
public enum SagaManualAction { Retry, Skip, ConfirmSucceeded, ConfirmNoEffect, Compensate }
public sealed record SagaSnapshot(OperationRecord Saga, IReadOnlyList<OperationRecord> Steps);
public interface ISagaCoordinator
{
    ValueTask<DurableOperationReceipt> StartAsync(SagaCommand command, CancellationToken token = default);
    ValueTask<OperationRecord> StartInTransactionAsync(IOperationTransaction transaction, SagaCommand command, CancellationToken token = default);
    ValueTask<SagaSnapshot?> GetAsync(string sagaId, string accessScope, CancellationToken token = default);
    ValueTask ApplyManualAsync(string sagaId, string stepId, string accessScope, long expectedSagaVersion,
        SagaManualAction action, string actor, string reason, CancellationToken token = default);
}
internal sealed record SagaDocument(int Format, SagaCommand Command, bool Compensating = false);
internal sealed record SagaStepDocument(int Format, SagaStepDefinition Definition, string ForwardKey, string CompensationKey,
    string? Output = null, string? LastReason = null, int? ManualAttemptLimit = null);
