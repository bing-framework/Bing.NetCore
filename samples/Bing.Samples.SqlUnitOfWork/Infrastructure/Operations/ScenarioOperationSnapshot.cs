using Bing.Samples.SqlUnitOfWork.Application;

namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

/// <summary>
/// 后台操作当前可查询的状态和业务结果。
/// </summary>
public sealed record ScenarioOperationSnapshot(
    string OperationId,
    string Scenario,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? FinishedAt = null,
    string? JobId = null,
    string? ErrorType = null,
    string? Error = null,
    ScenarioExecutionResult? Result = null,
    string? ExecutionOutcome = null,
    IReadOnlyList<ScenarioProcessEventEvidence>? ProcessEvents = null,
    IReadOnlyList<ScenarioProcessStepEvidence>? ProcessSteps = null,
    IReadOnlyList<ScenarioCallbackFailureEvidence>? CallbackFailures = null,
    ScenarioOperationIdentityEvidence? ExecutionIdentity = null);

public sealed record ScenarioOperationIdentityEvidence(
    long ServiceIdentity,
    long ScopeManagerIdentity,
    Guid RootScopeId,
    long DbContextIdentity,
    long QueryIdentity,
    bool ManagerCurrentIsRoot,
    bool RepositoryUsesSameContext,
    string? TraceId,
    string? TraceRootId,
    string? TraceParentId,
    string? TraceChildId,
    string? TenantId);

public sealed record ScenarioProcessEventEvidence(string EventId, string TraceId, string RootId,
    string Source, string Topic, string Scenario, DateTimeOffset RegisteredAt,
    DateTimeOffset? PublisherCallStartedAt = null, DateTimeOffset? PublisherReturnedAt = null);

public sealed record ScenarioProcessStepEvidence(long Sequence, string EventId, string Source, string Step,
    DateTimeOffset At);

public sealed record ScenarioCallbackFailureEvidence(string? Source, int? ResourceSequence, string Outcome,
    string ErrorType, string Error, DateTimeOffset At);
