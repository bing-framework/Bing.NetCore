using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>持有显式事务作用域的连接；不创建查询描述或 Builder。</summary>
internal sealed class SqlTransactionConnectionOwner : IDisposable, IAsyncDisposable
{
    private readonly bool _ownsConnection;
    private bool _disposed;

    internal SqlTransactionConnectionOwner(DatabaseContext context, ISqlProvider provider,
        IDbConnection connection, bool ownsConnection)
    {
        Context = DatabaseContextSnapshot.Create(context);
        Provider = provider;
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _ownsConnection = ownsConnection;
    }

    internal DatabaseContext Context { get; }
    internal ISqlProvider Provider { get; }
    internal IDbConnection Connection { get; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsConnection) Connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_ownsConnection)
            await SqlTransactionAsyncAdapter.DisposeAsync(Connection).ConfigureAwait(false);
    }
}
