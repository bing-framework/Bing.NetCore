using Bing.Data;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// SQL 子句运行上下文。
/// </summary>
/// <remarks>
/// 上下文描述子句绑定到某个 Builder 时使用的运行服务，不保存 Select、Join、Where、分页或其他查询状态。
/// </remarks>
public sealed record SqlClauseContext
{
    /// <summary>
    /// 当前 SQL Builder。
    /// </summary>
    public ISqlBuilder Builder { get; }

    /// <summary>
    /// SQL 提供程序。
    /// </summary>
    public ISqlProvider Provider { get; }

    /// <summary>
    /// SQL 方言。
    /// </summary>
    public IDialect Dialect => Provider.Dialect;

    /// <summary>
    /// 当前实体解析器。
    /// </summary>
    public IEntityResolver EntityResolver { get; }

    /// <summary>
    /// 当前实体别名注册器。
    /// </summary>
    public IEntityAliasRegister AliasRegister { get; }

    /// <summary>
    /// 当前参数管理器。
    /// </summary>
    public IParameterManager ParameterManager { get; }

    /// <summary>
    /// Builder 生命周期内固定的执行上下文。
    /// </summary>
    public SqlBuilderExecutionContext ExecutionContext { get; }

    /// <summary>
    /// SQL Builder 共享服务集合。
    /// </summary>
    public SqlBuilderServices Services { get; }

    /// <summary>
    /// 初始化一个 <see cref="SqlClauseContext"/> 类型的实例。
    /// </summary>
    /// <param name="builder">当前 SQL Builder。</param>
    /// <param name="provider">SQL 提供程序。</param>
    /// <param name="entityResolver">当前实体解析器。</param>
    /// <param name="aliasRegister">当前实体别名注册器。</param>
    /// <param name="parameterManager">当前参数管理器。</param>
    /// <param name="executionContext">固定执行上下文。</param>
    /// <param name="services">SQL Builder 共享服务集合。</param>
    internal SqlClauseContext(ISqlBuilder builder, ISqlProvider provider, IEntityResolver entityResolver,
        IEntityAliasRegister aliasRegister, IParameterManager parameterManager,
        SqlBuilderExecutionContext executionContext, SqlBuilderServices services)
    {
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        EntityResolver = entityResolver ?? throw new ArgumentNullException(nameof(entityResolver));
        AliasRegister = aliasRegister ?? throw new ArgumentNullException(nameof(aliasRegister));
        ParameterManager = parameterManager ?? throw new ArgumentNullException(nameof(parameterManager));
        ExecutionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 从既有分散依赖创建兼容运行上下文。
    /// </summary>
    /// <param name="builder">当前 SQL Builder。</param>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="entityResolver">实体解析器。</param>
    /// <param name="aliasRegister">实体别名注册器。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="entityMappingResolver">实体映射解析器。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="sqlParameterFactory">SQL 参数工厂。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="options">SQL 配置。</param>
    /// <param name="databaseContextResolver">数据库上下文解析器。</param>
    /// <param name="objectNameFormatter">SQL 对象名称格式化器。</param>
    /// <param name="crossDatabaseQueryValidator">跨数据库查询校验器。</param>
    /// <param name="tableReferenceValidator">SQL 表引用校验器。</param>
    /// <param name="entityModelMetadataProvider">实体模型原始元数据提供器。</param>
    /// <param name="databaseContext">固定数据库上下文。</param>
    /// <returns>包含默认依赖的兼容运行上下文。</returns>
    internal static SqlClauseContext Create(ISqlBuilder builder, IDialect dialect, IEntityResolver entityResolver,
        IEntityAliasRegister aliasRegister, IParameterManager parameterManager,
        IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        ISqlTableReferenceValidator tableReferenceValidator = null,
        IEntityModelMetadataProvider entityModelMetadataProvider = null, DatabaseContext databaseContext = null)
    {
        var services = new SqlBuilderServices(entityMappingResolver, databaseContextAccessor,
            sqlParameterFactory, metadataOptions, options, databaseContextResolver, objectNameFormatter,
            crossDatabaseQueryValidator, tableReferenceValidator, entityModelMetadataProvider);
        var resolvedDatabaseContext = DatabaseContextSnapshot.Create(databaseContext) ??
            services.DatabaseContextResolver.Resolve(services.Options) ??
            services.Options.GetDatabaseContext() ?? services.DatabaseContextAccessor?.Current ??
            services.MetadataOptions.DefaultDatabaseContext;
        return new SqlClauseContext(builder, new LegacySqlProvider(dialect), entityResolver, aliasRegister,
            parameterManager ?? new ParameterManager(dialect), new SqlBuilderExecutionContext(resolvedDatabaseContext), services);
    }

    /// <summary>
    /// 使用新的 Builder、别名注册器和参数管理器创建重绑定上下文。
    /// </summary>
    /// <param name="builder">重绑定后的 Builder。</param>
    /// <param name="entityResolver">重绑定后的实体解析器。</param>
    /// <param name="aliasRegister">重绑定后的别名注册器。</param>
    /// <param name="parameterManager">重绑定后的参数管理器。</param>
    /// <returns>保留共享服务和执行上下文的新运行上下文。</returns>
    public SqlClauseContext Rebind(ISqlBuilder builder, IEntityResolver entityResolver,
        IEntityAliasRegister aliasRegister, IParameterManager parameterManager) => new(builder, Provider,
        entityResolver, aliasRegister, parameterManager, ExecutionContext, Services);
}