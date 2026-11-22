using System.Data;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;
using Moq;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 MySQL 原生异步事务及写入命令中的 Provider 能力快照。</summary>
public class MySqlAotTransactionProfileTest
{
    [Fact]
    public void Profile_DeclaresNativeAsyncTransactionCallbacks()
    {
        var transaction = MySqlSqlProvider.Instance.Profile.Transaction;

        Assert.True(transaction.SupportsNativeAsyncBegin);
        Assert.True(transaction.SupportsNativeAsyncCommit);
        Assert.True(transaction.SupportsNativeAsyncRollback);
        Assert.NotNull(transaction.BeginAsync);
        Assert.NotNull(transaction.CommitAsync);
        Assert.NotNull(transaction.RollbackAsync);
    }

    [Fact]
    public void Profile_StaticCallbacks_DeclineCustomConnectionAndTransaction()
    {
        var transaction = MySqlSqlProvider.Instance.Profile.Transaction;
        var connection = new Mock<IDbConnection>().Object;
        var customTransaction = new Mock<IDbTransaction>().Object;

        Assert.Null(transaction.BeginAsync(connection, null, CancellationToken.None));
        Assert.Null(transaction.CommitAsync(customTransaction, CancellationToken.None));
        Assert.Null(transaction.RollbackAsync(customTransaction, CancellationToken.None));
    }

    [Fact]
    public void SqlWriteCommand_SnapshotsNativeAsyncTransactionCallbacks()
    {
        var transaction = MySqlSqlProvider.Instance.Profile.Transaction;
        var command = new SqlWriteCommand("Insert Into `transaction_probe` (`Id`) Values (@id)",
            Array.Empty<SqlParam>(), MySqlSqlProvider.Instance, SqlOperationKind.InsertValues);
        var snapshot = Assert.IsType<SqlProviderProfile>(command.ProviderProfile).Transaction;

        Assert.Same(transaction.BeginAsync, snapshot.BeginAsync);
        Assert.Same(transaction.CommitAsync, snapshot.CommitAsync);
        Assert.Same(transaction.RollbackAsync, snapshot.RollbackAsync);
    }
}
