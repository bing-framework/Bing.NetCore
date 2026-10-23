namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Scenarios;

/// <summary>
/// Sample 中公开的固定事务场景名称。
/// </summary>
public static class SqlUnitOfWorkScenarios
{
    public const string CrossLayerCommit = "cross-layer-commit";
    public const string CrossLayerRollback = "cross-layer-rollback";
    public const string MemoryEventImmediate = "memory-event-immediate";
    public const string MemoryEventFailure = "memory-event-failure";
    public const string MemoryEventAfterComplete = "memory-event-after-complete";
    public const string MemoryEventAfterCompleteRollback = "memory-event-after-complete-rollback";
    public const string MultiDatabaseCommit = "multi-database-commit";
    public const string MultiDatabaseRollback = "multi-database-rollback";
    public const string CapMultiSourceCommit = "cap-multi-source-commit";
    public const string CapMultiSourceRollback = "cap-multi-source-rollback";
    public const string CapMessageFirstCommit = "cap-message-first-commit";
    public const string CapEfFirstCommit = "cap-ef-first-commit";
    public const string CapDuplicateEvent = "cap-duplicate-event";
    public const string CapConsumerRetry = "cap-consumer-retry";
    public const string CapPartialCommit = "cap-partial-commit";
    public const string CaughtSqlException = "caught-sql-exception";
    public const string FactoryQueryLifetime = "factory-query-lifetime";
    public const string EfDapperCommit = "ef-dapper-commit";
    public const string EfDapperRollback = "ef-dapper-rollback";
    public const string EfCompleteWithoutSave = "ef-complete-without-save";
    public const string ScopeIsolationProbe = "scope-isolation-probe";
    public const string OperationSuccess = "operation-success";
    public const string OperationFailure = "operation-failure";
    public const string OperationCancellation = "operation-cancellation";
    public const string OperationEfTrackedOnly = "operation-ef-tracked-only";
    public const string OperationCapSuccess = "operation-cap-success";
    public const string OperationCapEfFirstSuccess = "operation-cap-ef-first-success";
    public const string OperationCapFailure = "operation-cap-failure";
    public const string OperationCapCancellation = "operation-cap-cancellation";
}
