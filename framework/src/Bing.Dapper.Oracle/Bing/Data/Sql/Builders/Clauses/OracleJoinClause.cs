using System;
using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

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
        ITableDatabase tableDatabase,
        IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null,
        SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null)
        : base(sqlBuilder, dialect, resolver, register, parameterManager, tableDatabase, null, entityMappingResolver,
            databaseContextAccessor, sqlParameterFactory, metadataOptions, options, databaseContextResolver,
            objectNameFormatter, crossDatabaseQueryValidator)
    {
    }

    /// <inheritdoc />
    protected override JoinItem CreateJoinItem(string joinType, string table, string schema, string alias, Type type = null) =>
        new JoinItem(joinType, table, schema, alias, false, false, type);
}
