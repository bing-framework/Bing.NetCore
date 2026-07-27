using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>SQL Server SQL 提供程序。</summary>
public sealed class SqlServerSqlProvider : ISqlProvider
{
    public static SqlServerSqlProvider Instance { get; } = new();
    private SqlServerSqlProvider() { }
    public DatabaseType DatabaseType => DatabaseType.SqlServer;
    public IDialect Dialect => SqlServerDialect.Instance;
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    public ISqlPaginationRenderer PaginationRenderer { get; } = new SqlServerPaginationRenderer();
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
    public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();
}

internal sealed class SqlServerPaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
}