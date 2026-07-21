using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
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
        , IEntityMappingResolver entityMappingResolver = null
        , IDatabaseContextAccessor databaseContextAccessor = null
        , ISqlParameterFactory sqlParameterFactory = null
        , SqlMetadataOptions metadataOptions = null
        , SqlOptions options = null
        , ISqlDatabaseContextResolver databaseContextResolver = null
        , ISqlObjectNameFormatter objectNameFormatter = null
        , ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null
        , ISqlTableReferenceValidator tableReferenceValidator = null)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, null, entityMappingResolver,
            databaseContextAccessor, sqlParameterFactory, metadataOptions, options, databaseContextResolver,
            objectNameFormatter, crossDatabaseQueryValidator, tableReferenceValidator)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, type: type);
}
