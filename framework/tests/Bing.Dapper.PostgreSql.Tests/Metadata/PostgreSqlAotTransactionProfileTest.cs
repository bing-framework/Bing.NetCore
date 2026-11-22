using System.Data;
using Bing.Data.Sql.Builders;
using Moq;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Mutations;

namespace Bing.Dapper.Tests.Metadata;

/// <summary>验证 PostgreSQL 原生异步事务能力及写入命令中的 Provider 档案快照。</summary>
public class PostgreSqlAotTransactionProfileTest
{
    [Fact]
    public void Profile_DeclaresNativeAsyncTransactionCallbacks()
    {
        var transaction = PostgreSqlSqlProvider.Instance.Profile.Transaction;

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
        var transaction = PostgreSqlSqlProvider.Instance.Profile.Transaction;
        var customConnection = new Mock<IDbConnection>().Object;
        var customTransaction = new Mock<IDbTransaction>().Object;

        Assert.Null(transaction.BeginAsync(customConnection, null, CancellationToken.None));
        Assert.Null(transaction.CommitAsync(customTransaction, CancellationToken.None));
        Assert.Null(transaction.RollbackAsync(customTransaction, CancellationToken.None));
    }

    [Fact]
    public void SqlWriteCommand_SnapshotsNativeAsyncTransactionCallbacks()
    {
        var providerTransaction = PostgreSqlSqlProvider.Instance.Profile.Transaction;
        var command = new SqlWriteCommand(
            "Insert Into \"transaction_probe\" (\"Id\") Values (@id)",
            Array.Empty<SqlParam>(),
            PostgreSqlSqlProvider.Instance,
            SqlOperationKind.InsertValues);
        var snapshot = Assert.IsType<SqlProviderProfile>(command.ProviderProfile).Transaction;

        Assert.Same(providerTransaction.BeginAsync, snapshot.BeginAsync);
        Assert.Same(providerTransaction.CommitAsync, snapshot.CommitAsync);
        Assert.Same(providerTransaction.RollbackAsync, snapshot.RollbackAsync);
    }
}
