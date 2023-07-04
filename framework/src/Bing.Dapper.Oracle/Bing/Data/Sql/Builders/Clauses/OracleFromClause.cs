using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// Oracle From子句
/// </summary>
public class OracleFromClause : FromClause
{
    /// <inheritdoc />
    public OracleFromClause(
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
    public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new OracleFromClause(builder, Dialect, Resolver, register, TableDatabase, Table);
    }
}
