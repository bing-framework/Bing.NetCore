using System.Reflection;
using System.Data;
using Bing.Data.Sql;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>
/// 查询与写入本地资源状态的结构契约测试。
/// </summary>
public sealed class SqlExecutionResourceCoreContractTest
{
    /// <summary>
    /// 测试目的：连接、事务和清理状态按 owner 嵌入，三类状态块仍相互分离。
    /// </summary>
    [Fact]
    public void OwnerStorage_ShouldContainConnectionTransactionAndCleanupState()
    {
        var assembly = typeof(SqlExecutionResources).Assembly;
        var resourcesType = typeof(SqlExecutionResources);
        var coreType = assembly.GetType("Bing.Data.Sql.SqlExecutionResourceCore");
        var coreField = resourcesType.GetField("_resourceCore", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(coreType);
        Assert.Null(coreField);
        Assert.True(coreType.IsValueType);
        foreach (var type in new[] { typeof(SqlQueryResourceOwner), typeof(SqlWriteResourceOwner) })
            Assert.Equal(coreType, type.GetField("_resourceCore", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);

        foreach (var fieldName in new[]
                 {
                     "_connection", "_transaction", "_connectionOwnership", "_transactionOwnership",
                     "_connectionSource", "_transactionId", "_transactionExecutionMode",
                     "_primaryReadTransactionStarted", "_executionLease", "_isDisposed", "_executionStateLock"
                 })
        {
            Assert.Null(resourcesType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        var connectionStateField = coreType.GetField("ConnectionState", BindingFlags.Instance | BindingFlags.NonPublic);
        var transactionStateField = coreType.GetField("TransactionState", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(connectionStateField);
        Assert.NotNull(transactionStateField);
        Assert.True(connectionStateField.FieldType.IsValueType);
        Assert.True(transactionStateField.FieldType.IsValueType);
        Assert.NotEqual(connectionStateField.FieldType, transactionStateField.FieldType);

        foreach (var fieldName in new[]
                 {
                     "Connection", "Transaction", "ConnectionOwnership", "TransactionOwnership",
                     "ConnectionSource", "TransactionId", "TransactionExecutionMode",
                     "PrimaryReadTransactionStarted", "TransactionScopeLease", "ExternalTransactionResolver"
                 })
        {
            Assert.Null(coreType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
        }

        var lifecycleStateField = coreType.GetField("LifecycleState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(lifecycleStateField);
        Assert.True(lifecycleStateField.FieldType.IsValueType);
        foreach (var fieldName in new[] { "ExecutionLease", "IsDisposed", "ExecutionStateLock" })
        {
            Assert.Null(coreType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic));
            Assert.NotNull(lifecycleStateField.FieldType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic));
        }

        Assert.Equal(typeof(IDbConnection), connectionStateField.FieldType
            .GetField("Connection", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
        Assert.Equal(typeof(IDbTransaction), transactionStateField.FieldType
            .GetField("Transaction", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType);
    }
}
