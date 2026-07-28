using Bing.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL Builder 共享服务集合。
/// </summary>
/// <remarks>
/// 保存可在 Builder、New 和 Clone 生命周期之间安全共享的服务引用，
/// 不声明所有被引用服务绝对不可变；不包含参数、别名、子句、分页、CTE、Union 或其它查询级可变状态。
/// </remarks>
public sealed class SqlBuilderServices
{
    /// <summary>
    /// 将实体成员解析为表、列和数据库映射元数据的解析器。
    /// </summary>
    public IEntityMappingResolver EntityMappingResolver { get; }

    /// <summary>
    /// 访问当前执行流数据库上下文的访问器。
    /// </summary>
    public IDatabaseContextAccessor DatabaseContextAccessor { get; }

    /// <summary>
    /// 根据字段元数据和数据库上下文创建 SQL 参数的工厂。
    /// </summary>
    public ISqlParameterFactory ParameterFactory { get; }

    /// <summary>
    /// 控制实体映射、字段转换和默认数据库上下文的元数据配置。
    /// </summary>
    public SqlMetadataOptions MetadataOptions { get; }

    /// <summary>
    /// 保存当前 Builder 使用的查询和数据库选项。
    /// </summary>
    public SqlOptions Options { get; }

    /// <summary>
    /// 按 SQL 选项、访问器和元数据配置解析数据库上下文的解析器。
    /// </summary>
    public ISqlDatabaseContextResolver DatabaseContextResolver { get; }

    /// <summary>
    /// 格式化数据库、架构、表和列等 SQL 对象名称的格式化器。
    /// </summary>
    public ISqlObjectNameFormatter ObjectNameFormatter { get; }

    /// <summary>
    /// 校验跨数据库查询是否满足当前 Provider 能力的校验器。
    /// </summary>
    public ISqlCrossDatabaseQueryValidator CrossDatabaseQueryValidator { get; }

    /// <summary>
    /// 校验结构化表引用完整性和命名约束的校验器。
    /// </summary>
    public ISqlTableReferenceValidator TableReferenceValidator { get; }

    /// <summary>
    /// 提供实体原始模型元数据以支持映射、过滤和对象名称解析的提供器。
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