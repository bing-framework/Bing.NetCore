using System.Data;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>显式事务连接的所有权和上下文隔离契约。</summary>
public sealed class SqlTransactionConnectionOwnerTest
{
    [Fact]
    public async Task OwnedConnection_ShouldDisposeOnceAndSnapshotContext()
    {
        var connection = new Mock<IDbConnection>();
        var context = new DatabaseContext { DbKey = "first", TenantId = "tenant" };
        var owner = new SqlTransactionConnectionOwner(context, null, connection.Object, true);
        context.DbKey = "changed";

        owner.Dispose();
        await owner.DisposeAsync();

        Assert.Equal("first", owner.Context.DbKey);
        connection.Verify(item => item.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExternalConnection_ShouldRemainOwnedByCaller()
    {
        var connection = new Mock<IDbConnection>();
        var owner = new SqlTransactionConnectionOwner(new DatabaseContext(), null, connection.Object, false);

        await owner.DisposeAsync();
        owner.Dispose();

        connection.Verify(item => item.Dispose(), Times.Never);
    }
}
