using System.Reflection;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 验证工作单元资源状态与协调职责已经从共享资源门面中拆出。
/// </summary>
public sealed class SqlUnitOfWorkResourceCoordinatorContractTest
{
    /// <summary>
    /// 测试目的：工作单元状态由独立协调器持有，资源门面只保留转发入口。
    /// </summary>
    [Fact]
    public void ExecutionResources_ShouldDelegateUnitOfWorkStateToCoordinator()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var resourcesType = typeof(SqlExecutionResources);
        var coordinatorType = assembly.GetType("Bing.Data.Sql.SqlUnitOfWorkResourceCoordinator");

        Assert.NotNull(coordinatorType);

        var coordinatorField = resourcesType.GetField(
            "_unitOfWorkCoordinator", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(coordinatorField);
        Assert.Equal(coordinatorType, coordinatorField.FieldType);
        Assert.True(coordinatorType.IsValueType);

        foreach (var fieldName in new[]
                 {
                     "_unitOfWorkRoot", "_unitOfWorkResource", "_unitOfWorkAffinityDecided",
                     "_unitOfWorkExplicitlyIndependent", "_explicitTransactionScopeMode"
                 })
        {
            Assert.Null(resourcesType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        foreach (var fieldName in new[]
                 {
                     "Root", "Resource", "AffinityDecided", "ExplicitlyIndependent",
                     "ExplicitTransactionScopeMode"
                 })
        {
            Assert.NotNull(coordinatorType.GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        foreach (var methodName in new[]
                 {
                     "AcquireResourceLease", "MarkPreCancelledExecution", "AttachResource",
                     "EnsureTransaction", "EnsureTransactionAsync", "MarkRollbackOnly",
                     "EnsureNoRuntimeBinding"
                 })
        {
            Assert.NotNull(coordinatorType.GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic));
        }
    }
}
