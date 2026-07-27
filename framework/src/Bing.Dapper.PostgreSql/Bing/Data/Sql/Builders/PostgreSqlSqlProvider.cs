using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>PostgreSQL SQL 提供程序。</summary>
public sealed class PostgreSqlSqlProvider : ISqlProvider, ISqlParameterLimitProvider
{
    public static PostgreSqlSqlProvider Instance { get; } = new();
    private PostgreSqlSqlProvider() { }
    /// <inheritdoc />
    public string Key => "bing.postgresql";
    public DatabaseType DatabaseType => DatabaseType.PgSql;
    public IDialect Dialect { get; } = PostgreSqlDialect.Instance;
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    public ISqlPaginationRenderer PaginationRenderer { get; } = new PostgreSqlPaginationRenderer();
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
    public IParamLiteralsResolver ParamLiteralsResolver { get; } = PostgreSqlParamLiteralsResolver.Instance;
    /// <inheritdoc />
    public int? MaxParameterCount => null;
}

internal sealed class PostgreSqlPaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}