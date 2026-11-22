using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSQL SQL 提供程序。
/// </summary>
public sealed class PostgreSqlSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 获取可在线程间安全共享的 PostgreSQL Provider 单例。
    /// </summary>
    public static PostgreSqlSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="PostgreSqlSqlProvider"/> 类型的实例。
    /// </summary>
    private PostgreSqlSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.postgresql";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.PgSql;

    /// <inheritdoc />
    public IDialect Dialect { get; } = PostgreSqlDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new PostgreSqlPaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } = PostgreSqlParamLiteralsResolver.Instance;

    /// <inheritdoc />
    public SqlProviderProfile Profile { get; } = new()
    {
        Query = new SqlProviderQueryCapabilities
        {
            Cte = SqlQueryCapabilityState.Supported,
            Union = SqlQueryCapabilityState.Supported,
            UnionAll = SqlQueryCapabilityState.Supported,
            Intersect = SqlQueryCapabilityState.Supported,
            Except = SqlQueryCapabilityState.Supported,
            RightJoin = SqlQueryCapabilityState.Supported,
            FullJoin = SqlQueryCapabilityState.Supported,
            Pagination = SqlQueryCapabilityState.Supported
        },
        Mutation = new SqlProviderMutationCapabilities
        {
            SupportsMultiRowValues = true,
            SupportsUpdateFrom = true,
            SupportsDeleteUsing = true,
            SupportsReturning = true
        },
        Execution = new SqlProviderExecutionCapabilities
        {
            SupportsMultipleResultSets = true,
            SupportsStreaming = true,
            SupportsCancellation = true
        },
        Transaction = new SqlProviderTransactionCapabilities
        {
            SupportsTransactions = true,
            SupportsNativeAsyncBegin = true,
            SupportsNativeAsyncCommit = true,
            SupportsNativeAsyncRollback = true,
#if NET6_0_OR_GREATER
            BeginAsync = (connection, isolation, token) => connection is NpgsqlConnection npgsql
                ? BeginTransactionAsync(npgsql, isolation, token) : null,
#endif
            CommitAsync = CommitTransactionAsync,
            RollbackAsync = RollbackTransactionAsync
        },
        Procedure = new SqlProviderProcedureCapabilities
        {
            SupportsStoredProcedures = false,
            StoredProceduresFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported,
            SupportsOutputParameters = false,
            OutputParametersFailureReason = SqlCapabilityFailureReason.DatabaseUnsupported
        },
        Limits = new SqlProviderLimits { MaxParameterCount = null }
    };

#if NET6_0_OR_GREATER
    private static async Task<IDbTransaction> BeginTransactionAsync(NpgsqlConnection connection, IsolationLevel? isolation,
        CancellationToken cancellationToken) => isolation.HasValue
        ? await connection.BeginTransactionAsync(isolation.Value, cancellationToken).ConfigureAwait(false)
        : await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#endif

    private static Task CommitTransactionAsync(IDbTransaction transaction, CancellationToken cancellationToken) =>
        transaction is NpgsqlTransaction npgsql ? npgsql.CommitAsync(cancellationToken) : null;

    private static Task RollbackTransactionAsync(IDbTransaction transaction, CancellationToken cancellationToken) =>
        transaction is NpgsqlTransaction npgsql ? npgsql.RollbackAsync(cancellationToken) : null;
}

/// <summary>
/// 渲染 PostgreSQL <c>Limit ... Offset ...</c> 分页语法。
/// </summary>
internal sealed class PostgreSqlPaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}
