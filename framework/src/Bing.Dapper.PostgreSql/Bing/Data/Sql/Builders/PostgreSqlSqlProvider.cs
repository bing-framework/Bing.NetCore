using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>PostgreSQL SQL 提供程序。</summary>
public sealed class PostgreSqlSqlProvider : ISqlProvider
{
    public static PostgreSqlSqlProvider Instance { get; } = new();
    private PostgreSqlSqlProvider() { }
    public DatabaseType DatabaseType => DatabaseType.PgSql;
    public IDialect Dialect => PostgreSqlDialect.Instance;
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    public ISqlPaginationRenderer PaginationRenderer { get; } = new PostgreSqlPaginationRenderer();
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
    public IParamLiteralsResolver ParamLiteralsResolver => PostgreSqlParamLiteralsResolver.Instance;
}

internal sealed class PostgreSqlPaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}