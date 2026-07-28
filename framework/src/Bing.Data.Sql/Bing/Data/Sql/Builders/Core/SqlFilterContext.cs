using Bing.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 过滤器解析与渲染上下文。
/// </summary>
public class SqlFilterContext
{
    /// <summary>
    /// 提供标识符、分页和表达式渲染规则的 SQL 方言。
    /// </summary>
    public IDialect Dialect { get; }

    /// <summary>
    /// 保存过滤器解析期间实体类型和表别名关联的注册器。
    /// </summary>
    public IEntityAliasRegister EntityAliasRegister { get; }

    /// <summary>
    /// 解析实体字段、表和数据库元数据的提供器。
    /// </summary>
    public IEntityModelMetadataProvider EntityModelMetadataProvider { get; }

    /// <summary>
    /// 收集过滤条件生成参数的参数管理器。
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// 提供当前 Builder 子句访问能力的访问器。
    /// </summary>
    public ISqlPartAccessor ClauseAccessor { get; }

    /// <summary>
    /// 将实体成员解析为 SQL 映射元数据的解析器。
    /// </summary>
    public IEntityMappingResolver EntityMappingResolver { get; }

    /// <summary>
    /// 访问当前执行流数据库上下文的访问器。
    /// </summary>
    public IDatabaseContextAccessor DatabaseContextAccessor { get; }

    /// <summary>
    /// 控制实体映射和数据库默认值的 SQL 元数据配置。
    /// </summary>
    public SqlMetadataOptions MetadataOptions { get; }

    /// <summary>
    /// 当前 Builder 的 SQL 选项。
    /// </summary>
    public SqlOptions Options { get; }

    /// <summary>
    /// 根据选项和环境解析数据库上下文的解析器。
    /// </summary>
    public ISqlDatabaseContextResolver DatabaseContextResolver { get; }

    /// <summary>
    /// Builder 生命周期内固定的数据库上下文。
    /// </summary>
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// 当前用于过滤器解析和表路由的数据库上下文。
    /// </summary>
    public DatabaseContext DatabaseContext =>
        _databaseContext ?? DatabaseContextResolver?.Resolve(Options) ?? Options.GetDatabaseContext() ??
        DatabaseContextAccessor?.Current ?? MetadataOptions?.DefaultDatabaseContext;

    /// <summary>
    /// 初始化 SQL 过滤器解析与渲染上下文。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="entityAliasRegister">实体别名注册器。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="clause">SQL 子句访问器。</param>
    /// <param name="entityMappingResolver">实体映射解析器。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="options">SQL 选项。</param>
    /// <param name="databaseContextResolver">SQL 数据库上下文解析器。</param>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器。</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文。</param>
    public SqlFilterContext(IDialect dialect, IEntityAliasRegister entityAliasRegister, IParameterManager parameterManager,
        ISqlPartAccessor clause,
        IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null,
        SqlMetadataOptions metadataOptions = null,
        SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null, DatabaseContext databaseContext = null)
    {
        EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
        EntityModelMetadataProvider = entityModelMetadataProvider ?? new DefaultEntityModelMetadataProvider();
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
        _databaseContext = DatabaseContextSnapshot.Create(databaseContext);
    }
}