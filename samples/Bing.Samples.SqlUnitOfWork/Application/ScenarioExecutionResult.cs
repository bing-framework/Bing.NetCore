using Bing.Samples.SqlUnitOfWork.Infrastructure.Operations;

namespace Bing.Samples.SqlUnitOfWork.Application;

/// <summary>
/// 一次同步场景的事务内观察结果。
/// </summary>
public sealed record ScenarioExecutionResult(
    string OperationId,
    string Scenario,
    Guid ScopeId,
    bool QueryInstancesAreIndependent,
    int ApplicationVisibleOrderCount,
    int RepositoryVisibleOrderCount,
    int ReportingVisibleRecordCount,
    int FactoryQueriesExecuted = 0,
    int EfSaveChangesAffected = 0,
    string? Detail = null,
    string Outcome = "Committed",
    ScenarioOperationIdentityEvidence? ExecutionIdentity = null);
