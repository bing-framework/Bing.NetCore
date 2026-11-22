using System.Data;
using System.Reflection;
using Bing.Data;
using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>验证查询和写入的物理资源存储与生命周期相互独立。</summary>
public sealed class SqlResourceStorageIsolationTest
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>通用算法基类不应再实例化或保存独立的引用型资源核心。</summary>
    [Fact]
    public void CommonResources_ShouldNotOwnConcreteStorage()
    {
        Assert.True(typeof(SqlExecutionResources).IsAbstract);
        Assert.True(typeof(SqlExecutionResourceCore).IsValueType);
        Assert.Null(typeof(SqlExecutionResources).GetField("_resourceCore", Fields));
    }

    /// <summary>两类 owner 直接嵌入状态，不再引用另一个具体资源门面。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceOwner_ShouldEmbedItsOwnState(bool query)
    {
        var type = query ? typeof(SqlQueryResourceOwner) : typeof(SqlWriteResourceOwner);
        Assert.True(typeof(SqlExecutionResources).IsAssignableFrom(type));
        Assert.Equal(typeof(SqlExecutionResourceCore), type.GetField("_resourceCore", Fields)?.FieldType);
        Assert.Null(type.GetField("_resources", Fields));
    }

    /// <summary>Query、多结果和 Executor 构造时只创建自己的资源所有者。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Entrypoint_ShouldUseItsOwnResourceOwner(int kind)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var connection = new Mock<IDbConnection>();
        var options = new SqlOptions { Connection = connection.Object };
        using SqlExecutionBase execution = kind switch
        {
            0 => new Query(services, options),
            1 => new Multiple(services, options),
            _ => new Writer(services, options)
        };
        var resources = GetResources(execution);

        Assert.Equal(kind == 2 ? typeof(SqlWriteResourceOwner) : typeof(SqlQueryResourceOwner), resources.GetType());
        Assert.Same(connection.Object, resources.GetExecutionConnection());
        Assert.Equal(SqlResourceOwnership.External, resources.ConnectionOwnership);
        execution.Dispose();
        connection.Verify(value => value.Dispose(), Times.Never);
    }

    /// <summary>一侧事务清理即使抛错，也不修改另一侧的事务、连接或执行租约。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DisposeQuery_ShouldNotChangeIndependentWriteResources(bool asynchronous, bool fails)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var queryConnection = Connection();
        var writeConnection = Connection();
        var queryTransaction = Transaction(queryConnection.Object);
        var writeTransaction = Transaction(writeConnection.Object);
        using var query = new Query(services, new SqlOptions { Connection = queryConnection.Object });
        using var writer = new Writer(services, new SqlOptions { Connection = writeConnection.Object });
        var queryResources = GetResources(query);
        var writeResources = GetResources(writer);
        queryResources._transaction = queryTransaction.Object;
        writeResources._transaction = writeTransaction.Object;
        queryResources._connectionOwnership = SqlResourceOwnership.Owned;
        writeResources._connectionOwnership = SqlResourceOwnership.Owned;
        var failure = new InvalidOperationException("query dispose");
        if (fails) queryTransaction.Setup(value => value.Dispose()).Throws(failure);

        async Task DisposeQuery()
        {
            if (asynchronous) await ((IAsyncDisposable)query).DisposeAsync();
            else query.Dispose();
        }
        if (fails) Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(DisposeQuery));
        else await DisposeQuery();

        Assert.Same(writeConnection.Object, writeResources.GetExecutionConnection());
        Assert.Same(writeTransaction.Object, writeResources.GetExecutionTransaction());
        using (writeResources.AcquireExecutionLease())
            Assert.Throws<InvalidOperationException>(writer.Dispose);
        writer.Dispose();
        writer.Dispose();
        query.Dispose();
        queryTransaction.Verify(value => value.Dispose(), Times.Once);
        queryConnection.Verify(value => value.Dispose(), Times.Once);
        writeTransaction.Verify(value => value.Dispose(), Times.Once);
        writeConnection.Verify(value => value.Dispose(), Times.Once);
        Assert.Throws<ObjectDisposedException>(() => queryResources.AcquireExecutionLease());
        Assert.Throws<ObjectDisposedException>(() => writeResources.AcquireExecutionLease());
    }

    private static SqlExecutionResources GetResources(SqlExecutionBase execution) =>
        (SqlExecutionResources)typeof(SqlExecutionBase).GetField("_resources", Fields)!.GetValue(execution)!;

    private static Mock<IDbConnection> Connection()
    {
        var connection = new Mock<IDbConnection>();
        connection.SetupGet(value => value.State).Returns(ConnectionState.Open);
        return connection;
    }

    private static Mock<IDbTransaction> Transaction(IDbConnection connection)
    {
        var transaction = new Mock<IDbTransaction>();
        transaction.SetupGet(value => value.Connection).Returns(connection);
        return transaction;
    }

    private sealed class Query(IServiceProvider services, SqlOptions options) : SqlQueryBase(services, options);
    private sealed class Writer(IServiceProvider services, SqlOptions options) : SqlExecutorBase(services, options);
    private sealed class Multiple(IServiceProvider services, SqlOptions options) : SqlMultipleQueryExecutorBase(services, options);
}
