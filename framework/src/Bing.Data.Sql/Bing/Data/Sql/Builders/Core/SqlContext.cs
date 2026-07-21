using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql执行上下文
/// </summary>
public class SqlContext
{
    /// <summary>
    /// Sql方言
    /// </summary>
    public IDialect Dialect { get; }

    /// <summary>
    /// 实体别名注册器
    /// </summary>
    public IEntityAliasRegister EntityAliasRegister { get; }

    /// <summary>
    /// 实体模型原始元数据提供器
    /// </summary>
    public IEntityModelMetadataProvider EntityModelMetadataProvider { get; }

    /// <summary>
    /// 参数管理器
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// Sql子句访问器
    /// </summary>
    public ISqlPartAccessor ClauseAccessor { get; }

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    public IEntityMappingResolver EntityMappingResolver { get; }

    /// <summary>
    /// 数据库上下文访问器
    /// </summary>
    public IDatabaseContextAccessor DatabaseContextAccessor { get; }

    /// <summary>
    /// Sql 元数据配置
    /// </summary>
    public SqlMetadataOptions MetadataOptions { get; }

    /// <summary>
    /// Sql 配置
    /// </summary>
    public SqlOptions Options { get; }

    /// <summary>
    /// SQL 数据库上下文解析器
    /// </summary>
    public ISqlDatabaseContextResolver DatabaseContextResolver { get; }

    /// <summary>
    /// 当前数据库上下文
    /// </summary>
    public DatabaseContext DatabaseContext =>
        DatabaseContextResolver?.Resolve(Options) ?? Options.GetDatabaseContext() ?? DatabaseContextAccessor?.Current ??
        MetadataOptions?.DefaultDatabaseContext;

    /// <summary>
    /// 初始化一个<see cref="SqlContext"/>类型的实例
    /// </summary>
    /// <param name="dialect">Sql方言</param>
    /// <param name="entityAliasRegister">实体别名注册器</param>
    /// <param name="parameterManager">参数管理器</param>
    /// <param name="clause">Sql子句访问器</param>
    /// <param name="entityMappingResolver">实体映射解析器</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器</param>
    /// <param name="metadataOptions">Sql元数据配置</param>
    /// <param name="options">Sql 配置</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器</param>
    public SqlContext(IDialect dialect, IEntityAliasRegister entityAliasRegister, IParameterManager parameterManager,
        ISqlPartAccessor clause,
        IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null)
    {
        EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
        EntityModelMetadataProvider = entityModelMetadataProvider ?? new DefaultEntityMetadata();
        Dialect = dialect;
        ParameterManager = parameterManager;
        ClauseAccessor = clause ?? throw new ArgumentNullException(nameof(clause));
        MetadataOptions = metadataOptions ?? new SqlMetadataOptions();
        EntityMappingResolver = entityMappingResolver ?? new DefaultEntityMappingResolver(
            databaseContextAccessor: databaseContextAccessor, options: MetadataOptions,
            entityModelMetadataProvider: EntityModelMetadataProvider);
        DatabaseContextAccessor = databaseContextAccessor;
        Options = options;
        DatabaseContextResolver = databaseContextResolver ?? new DefaultSqlDatabaseContextResolver(databaseContextAccessor,
            MetadataOptions);
    }
}
