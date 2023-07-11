using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Sqlite 表连接子句
/// </summary>
public class SqliteJoinClause : JoinClause
{
    /// <inheritdoc />
    public SqliteJoinClause(ISqlBuilder sqlBuilder
        , IDialect dialect
        , IEntityResolver resolver
        , IEntityAliasRegister register
        , IParameterManager parameterManager
        , ITableDatabase tableDatabase)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, tableDatabase)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, false, false, type);
}
