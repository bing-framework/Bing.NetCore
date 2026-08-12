using Bing.Data;
using Bing.Data.Filters;
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
    public ISqlQueryClauseAccessor ClauseAccessor { get; }

    /// <summary>
    /// 当前查询图中的根表源实例。
    /// </summary>
    public IReadOnlyList<TableSource> RootSources { get; }

    /// <summary>
    /// 当前查询图中的类型化 Join 表源实例。
    /// </summary>
    public IReadOnlyList<TableSource> JoinSources { get; }

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
    /// 当前异步执行流共享的数据过滤状态。
    /// </summary>
    public IDataFilter DataFilter { get; }

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
    /// <param name="services">SQL Builder 共享服务集合。</param>
    /// <param name="databaseContext">Builder 生命周期内固定的数据库上下文。</param>
    public SqlFilterContext(IDialect dialect, IEntityAliasRegister entityAliasRegister, IParameterManager parameterManager,
        ISqlQueryClauseAccessor clause, SqlBuilderServices services, DatabaseContext databaseContext = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        EntityAliasRegister = entityAliasRegister ?? new EntityAliasRegister();
        EntityModelMetadataProvider = services.EntityModelMetadataProvider;
        Dialect = dialect;
        ParameterManager = parameterManager;
        ClauseAccessor = clause ?? throw new ArgumentNullException(nameof(clause));
        RootSources = (clause.FromClause as Clauses.FromClause)?.Sources ?? Array.Empty<TableSource>();
        JoinSources = (clause.JoinClause as Clauses.JoinClause)?.GetTypedSources() ?? Array.Empty<TableSource>();
        MetadataOptions = services.MetadataOptions;
        EntityMappingResolver = services.EntityMappingResolver;
        DatabaseContextAccessor = services.DatabaseContextAccessor;
        Options = services.Options;
        DatabaseContextResolver = services.DatabaseContextResolver;
        DataFilter = services.DataFilter;
        _databaseContext = DatabaseContextSnapshot.Create(databaseContext);
    }
}