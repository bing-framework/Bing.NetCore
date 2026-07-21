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
        SqlItem table = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        Bing.Data.Enums.DatabaseType? providerDatabaseType = null,
        ISqlTableReferenceValidator tableReferenceValidator = null)
        : base(builder, dialect, resolver, register, table, objectNameFormatter, providerDatabaseType,
            tableReferenceValidator)
    {
    }

    /// <inheritdoc />
    public override IFromClause Clone(ISqlBuilder builder, IEntityAliasRegister register)
    {
        if (register != null)
            register.FromType = Register.FromType;
        return new OracleFromClause(builder, Dialect, Resolver, register, Table?.Clone(), ObjectNameFormatter,
            ProviderDatabaseType, TableReferenceValidator);
    }
}
