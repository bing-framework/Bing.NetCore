using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Sqlite From子句
/// </summary>
public class SqliteFromClause : FromClause
{
    /// <inheritdoc />
    public SqliteFromClause(
        ISqlBuilder builder,
        IDialect dialect,
        IEntityResolver resolver,
        IEntityAliasRegister register,
        ITableDatabase tableDatabase,
        SqlItem table = null)
        : base(builder, dialect, resolver, register, tableDatabase, table)
    {
    }

    /// <inheritdoc />
    protected override SqlItem CreateSqlItem(string table, string schema, string alias) =>
        new SqlItem(table, schema, alias, false, false);

    /// <inheritdoc />
    public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new SqliteFromClause(builder, Dialect, Resolver, register, TableDatabase, Table);
    }
}
