using Bing.Data;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql Sql生成器
/// </summary>
public class PostgreSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个<see cref="PostgreSqlBuilder"/>类型的实例
    /// </summary>
    /// <param name="metadata">实体元数据解析器</param>
    /// <param name="tableDatabase">表数据库</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="sqlParameterFactory">Sql 参数工厂</param>
    /// <param name="metadataOptions">Sql 元数据配置</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="tableReferenceResolver">SQL 表引用解析器</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器</param>
    public PostgreSqlBuilder(IEntityMetadata metadata = null, ITableDatabase tableDatabase = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlTableReferenceResolver tableReferenceResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null)
        : base(metadata, tableDatabase, parameterManager, entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions, options, databaseContextResolver, tableReferenceResolver,
            objectNameFormatter, crossDatabaseQueryValidator) { }

    /// <inheritdoc />
    protected override IDialect GetDialect() => PostgreSqlDialect.Instance;

    /// <inheritdoc />
    public override ISqlBuilder Clone()
    {
        var sqlBuilder = new PostgreSqlBuilder();
        sqlBuilder.Clone(this);
        return sqlBuilder;
    }

    /// <inheritdoc />
    public override ISqlBuilder New() => new PostgreSqlBuilder(EntityMetadata, TableDatabase, ParameterManager,
        EntityMappingResolver, DatabaseContextAccessor, SqlParameterFactory, MetadataOptions, Options,
        DatabaseContextResolver, TableReferenceResolver, ObjectNameFormatter, CrossDatabaseQueryValidator);

    /// <inheritdoc />
    protected override string CreateLimitSql() => $"Limit {GetLimitParam()} OFFSET {GetOffsetParam()}";

    /// <inheritdoc />
    protected override IParamLiteralsResolver GetParamLiteralsResolver() => PostgreSqlParamLiteralsResolver.Instance;
}
