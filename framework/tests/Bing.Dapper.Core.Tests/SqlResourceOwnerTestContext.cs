using System.Data;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Bing.Dapper.Core.Tests;

/// <summary>为资源所有者行为测试提供可计数的连接与事务。</summary>
internal sealed class SqlResourceOwnerTestContext : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    internal Mock<IDbConnection> Connection { get; } = new();
    internal Mock<IDbTransaction> Transaction { get; } = new();
    internal SqlExecutionResources Resources { get; }

    internal SqlResourceOwnerTestContext(bool primaryRead = false, bool query = true)
    {
        var state = ConnectionState.Closed;
        Connection.SetupGet(value => value.State).Returns(() => state);
        Connection.Setup(value => value.Open()).Callback(() => state = ConnectionState.Open);
        Connection.Setup(value => value.BeginTransaction()).Returns(Transaction.Object);
        Connection.Setup(value => value.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(Transaction.Object);
        Transaction.SetupGet(value => value.Connection).Returns(Connection.Object);
        Transaction.SetupGet(value => value.IsolationLevel).Returns(IsolationLevel.ReadCommitted);
        var options = new SqlOptions { Connection = Connection.Object };
        options.SetDatabaseContext(new DatabaseContext
        {
            ReadPreference = primaryRead ? SqlReadPreference.Primary : SqlReadPreference.Default,
            DataSource = new SqlDataSourceDescriptor
            {
                PrimaryReadStrategy = primaryRead ? PrimaryReadStrategy.Transaction : PrimaryReadStrategy.None
            }
        });
        var provider = new Mock<ISqlProvider>();
        provider.SetupGet(value => value.Key).Returns("test.resource-owner");
        var capabilities = new SqlProviderTransactionCapabilities { SupportsTransactions = true };
        Resources = query
            ? new SqlQueryResourceOwner(_services, () => options, () => provider.Object,
                () => null, () => false, () => capabilities, _ => { })
            : new SqlWriteResourceOwner(_services, () => options, () => provider.Object,
                () => null, () => false, () => capabilities, _ => { });
    }

    /// <summary>验证普通查询及外部事务参与者没有擅自控制事务。</summary>
    internal void VerifyNoTransactionLifecycle()
    {
        Connection.Verify(value => value.BeginTransaction(), Times.Never);
        Connection.Verify(value => value.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
        Transaction.Verify(value => value.Commit(), Times.Never);
        Transaction.Verify(value => value.Rollback(), Times.Never);
        Transaction.Verify(value => value.Dispose(), Times.Never);
    }

    public void Dispose()
    {
        Resources.Dispose();
        _services.Dispose();
    }
}
