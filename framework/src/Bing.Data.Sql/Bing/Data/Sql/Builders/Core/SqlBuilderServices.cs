using Bing.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL Builder 共享服务集合。
/// </summary>
/// <remarks>
/// 该对象只保存可在 Builder、New 和 Clone 间共享的不可变服务和配置，
/// 不包含参数、别名、子句、分页、CTE、Union 或其它查询状态。
/// </remarks>
public sealed class SqlBuilderServices
{
    /// <summary>
    /// 实体映射解析器。
    /// </summary>
    public IEntityMappingResolver EntityMappingResolver { get; }

    /// <summary>
    /// 数据库上下文访问器。
    /// </summary>
    public IDatabaseContextAccessor DatabaseContextAccessor { get; }

    /// <summary>
    /// SQL 参数工厂。
    /// </summary>
    public ISqlParameterFactory ParameterFactory { get; }

    /// <summary>
    /// SQL 元数据配置。
    /// </summary>
    public SqlMetadataOptions MetadataOptions { get; }

    /// <summary>
    /// SQL 配置。
    /// </summary>
    public SqlOptions Options { get; }

    /// <summary>
    /// 数据库上下文解析器。
    /// </summary>
    public ISqlDatabaseContextResolver DatabaseContextResolver { get; }

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    public ISqlObjectNameFormatter ObjectNameFormatter { get; }

    /// <summary>
    /// 跨数据库查询校验器。
    /// </summary>
    public ISqlCrossDatabaseQueryValidator CrossDatabaseQueryValidator { get; }

    /// <summary>
    /// SQL 表引用校验器。
    /// </summary>
    public ISqlTableReferenceValidator TableReferenceValidator { get; }

    /// <summary>
    /// 实体模型原始元数据提供器。
    /// </summary>
    public IEntityModelMetadataProvider EntityModelMetadataProvider { get; }

    /// <summary>
    /// 初始化一个 <see cref="SqlBuilderServices"/> 类型的实例，并填充默认服务。
    /// </summary>
    /// <param name="entityMappingResolver">实体映射解析器。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="parameterFactory">SQL 参数工厂。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="options">SQL 配置。</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器。</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器。</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器。</param>
    /// <param name="tableReferenceValidator">SQL 表引用校验器。</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器。</param>
    public SqlBuilderServices(IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory parameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        ISqlTableReferenceValidator tableReferenceValidator = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null)
    {
        MetadataOptions = metadataOptions ?? new SqlMetadataOptions();
        Options = options;
        DatabaseContextAccessor = databaseContextAccessor;
        EntityModelMetadataProvider = entityModelMetadataProvider ?? new DefaultEntityModelMetadataProvider();
        DatabaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(
            databaseContextAccessor, MetadataOptions);
        EntityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(
            databaseContextAccessor: databaseContextAccessor, options: MetadataOptions,
            entityModelMetadataProvider: EntityModelMetadataProvider);
        ObjectNameFormatter = objectNameFormatter ?? new DefaultSqlObjectNameFormatter();
        CrossDatabaseQueryValidator = crossDatabaseQueryValidator ?? new DefaultSqlCrossDatabaseQueryValidator();
        TableReferenceValidator = tableReferenceValidator ?? new DefaultSqlTableReferenceValidator();
        ParameterFactory = parameterFactory ?? new DefaultSqlParameterFactory(
            new DefaultFieldValueConverterSelector(null, MetadataOptions), databaseContextAccessor, MetadataOptions);
    }

    /// <summary>
    /// 创建使用默认服务的共享服务集合。
    /// </summary>
    /// <returns>共享服务集合。</returns>
    public static SqlBuilderServices CreateDefault() => new();
}