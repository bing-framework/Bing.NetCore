using System;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Matedatas;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Oracle 表连接子句
/// </summary>
public class OracleJoinClause : JoinClause
{
    /// <inheritdoc />
    public OracleJoinClause(
        ISqlBuilder sqlBuilder,
        IDialect dialect,
        IEntityResolver resolver,
        IEntityAliasRegister register,
        IParameterManager parameterManager,
        ITableDatabase tableDatabase)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, tableDatabase)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, false, false, type);
}
