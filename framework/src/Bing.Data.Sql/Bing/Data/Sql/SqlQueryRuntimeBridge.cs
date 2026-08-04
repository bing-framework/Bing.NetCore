using System.Data;
using System.Runtime.CompilerServices;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 查询内部运行时控制器。
/// </summary>
internal interface ISqlQueryRuntimeController
{
    IDbConnection GetOrCreateConnection();
    IDbTransaction GetCurrentTransaction();
    string GetCurrentTransactionId();
    void BindOwnedConnection(IDbConnection connection, SqlConnectionSource source);
    void BindExternalConnection(IDbConnection connection, SqlConnectionSource source);
    void BindExternalTransaction(IDbTransaction transaction, string transactionId = null);
    void BindExternalTransactionResolver(Func<IDbTransaction> resolver);
    void BindTransactionScope(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
        ISqlTransactionScopeLease lease);
    void BindEntityMappingResolver(IEntityMappingResolver resolver);
}

/// <summary>
/// SQL 查询内部运行时 bridge。
/// </summary>
internal static class SqlQueryRuntimeBridge
{
    private static readonly ConditionalWeakTable<ISqlQuery, ISqlQueryRuntimeController> Controllers = new();

    internal static void Register(ISqlQuery query, ISqlQueryRuntimeController controller)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));
        Controllers.Add(query, controller);
    }

    internal static void Remove(ISqlQuery query)
    {
        if (query != null)
            Controllers.Remove(query);
    }

    internal static IDbConnection GetOrCreateConnection(ISqlQuery query) => GetController(query).GetOrCreateConnection();

    internal static IDbTransaction GetCurrentTransaction(ISqlQuery query) => GetController(query).GetCurrentTransaction();

    internal static string GetCurrentTransactionId(ISqlQuery query) => GetController(query).GetCurrentTransactionId();

    internal static void BindOwnedConnection(ISqlQuery query, IDbConnection connection, SqlConnectionSource source) =>
        GetController(query).BindOwnedConnection(connection, source);

    internal static void BindExternalConnection(ISqlQuery query, IDbConnection connection, SqlConnectionSource source) =>
        GetController(query).BindExternalConnection(connection, source);

    internal static void BindExternalTransaction(ISqlQuery query, IDbTransaction transaction, string transactionId = null) =>
        GetController(query).BindExternalTransaction(transaction, transactionId);

    internal static void BindExternalTransactionResolver(ISqlQuery query, Func<IDbTransaction> resolver) =>
        GetController(query).BindExternalTransactionResolver(resolver);

    internal static void BindTransactionScope(ISqlQuery query, DatabaseContext context, IDbConnection connection,
        IDbTransaction transaction, ISqlTransactionScopeLease lease) =>
        GetController(query).BindTransactionScope(context, connection, transaction, lease);

    internal static void BindEntityMappingResolver(ISqlQuery query, IEntityMappingResolver resolver) =>
        GetController(query).BindEntityMappingResolver(resolver);

    private static ISqlQueryRuntimeController GetController(ISqlQuery query)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        return Controllers.TryGetValue(query, out var controller)
            ? controller
            : throw new InvalidOperationException("当前 SQL 查询对象不支持框架内部运行时资源绑定。");
    }
}