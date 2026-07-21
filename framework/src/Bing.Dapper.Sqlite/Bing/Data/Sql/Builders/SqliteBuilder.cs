using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sqlite Sql生成器
/// </summary>
public class SqliteBuilder : SqlBuilderBase
{
    /// <inheritdoc />
    protected override DatabaseType ProviderDatabaseType => DatabaseType.Sqlite;

    /// <summary>
    /// 初始化一个<see cref="SqliteBuilder"/>类型的实例
    /// </summary>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器</param>
    /// <param name="tableReferenceValidator">SQL 表引用验证器</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器</param>
    public SqliteBuilder(IParameterManager parameterManager = null
        , IEntityMappingResolver entityMappingResolver = null
        , IDatabaseContextAccessor databaseContextAccessor = null
        , ISqlParameterFactory sqlParameterFactory = null
        , SqlMetadataOptions metadataOptions = null
        , SqlOptions options = null
        , ISqlDatabaseContextResolver databaseContextResolver = null
        , ISqlObjectNameFormatter objectNameFormatter = null
        , ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null
        , ISqlTableReferenceValidator tableReferenceValidator = null
        , IEntityModelMetadataProvider entityModelMetadataProvider = null)
        : base(parameterManager, entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions, options, databaseContextResolver,
            objectNameFormatter, crossDatabaseQueryValidator, tableReferenceValidator, entityModelMetadataProvider)
    {
    }

    /// <inheritdoc />
    protected override IDialect GetDialect() => SqliteDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new SqliteBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new SqliteBuilder(ParameterManager, EntityMappingResolver,
        DatabaseContextAccessor, SqlParameterFactory, MetadataOptions, Options, DatabaseContextResolver,
        ObjectNameFormatter, CrossDatabaseQueryValidator, TableReferenceValidator, EntityModelMetadataProvider);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IFromClause CreateFromClause() =>
        new SqliteFromClause(this, GetDialect(), EntityResolver, AliasRegister, null, ObjectNameFormatter,
            ProviderDatabaseType, TableReferenceValidator);

    /// <inheritdoc />
    protected override IJoinClause CreateJoinClause() =>
        new SqliteJoinClause(this, GetDialect(), EntityResolver, AliasRegister, ParameterManager,
            EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions, Options,
            DatabaseContextResolver, ObjectNameFormatter, CrossDatabaseQueryValidator, TableReferenceValidator);

    /// <inheritdoc />
    protected override string GetCteKeyWord() => "With Recursive";
}
