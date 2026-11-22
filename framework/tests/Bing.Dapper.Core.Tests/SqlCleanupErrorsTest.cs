using System.Data;
using Bing.Data.Sql;
using Moq;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>成功清理保持空集合，多个清理失败按发生顺序保留。</summary>
public sealed class SqlCleanupErrorsTest
{
    [Fact]
    public void Cleanup_WhenSuccessful_ShouldLeaveErrorsEmpty()
    {
        var errors = new SqlCleanupErrors();
        var resource = new Mock<IDisposable>(MockBehavior.Strict);
        resource.Setup(item => item.Dispose());
        var connection = new Mock<IDbConnection>(MockBehavior.Strict);
        connection.Setup(item => item.Close());
        SqlCleanupErrors.Dispose(errors, resource.Object);
        SqlCleanupErrors.Close(errors, connection.Object);
        Assert.Empty(errors);
        resource.Verify(item => item.Dispose(), Times.Once);
        connection.Verify(item => item.Close(), Times.Once);
    }

    [Fact]
    public void Cleanup_WhenBothFail_ShouldPreserveExceptionsInOrder()
    {
        var errors = new SqlCleanupErrors();
        var first = new InvalidOperationException("reader disposal");
        var second = new InvalidOperationException("connection close");
        var resource = new Mock<IDisposable>();
        resource.Setup(item => item.Dispose()).Throws(first);
        var connection = new Mock<IDbConnection>();
        connection.Setup(item => item.Close()).Throws(second);
        SqlCleanupErrors.Dispose(errors, resource.Object);
        SqlCleanupErrors.Close(errors, connection.Object);
        Assert.Equal(new Exception[] { first, second }, errors);
    }
}
