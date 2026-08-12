using Bing.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Tests.Samples;

/// <summary>
/// 测试Sql生成器
/// </summary>
public class TestSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 获取当前 Builder 的共享服务。
    /// </summary>
    public SqlBuilderServices SharedServices => Services;

    /// <summary>
    /// 创建当前 Builder 的 Clause 运行上下文。
    /// </summary>
    /// <returns>Clause 运行上下文。</returns>
    public SqlClauseContext CreateCurrentClauseContext() => CreateClauseContext();

    /// <summary>
    /// 获取当前 Builder 的类型化 Join 表源快照。
    /// </summary>
    public IReadOnlyList<TableSource> GetTypedJoinSources() =>
        (JoinClause as Bing.Data.Sql.Builders.Clauses.JoinClause)?.GetTypedSources() ?? Array.Empty<TableSource>();

    /// <summary>
    /// 渲染子查询并合并参数，仅用于验证子查询组合边界。
    /// </summary>
    /// <param name="builder">待渲染的子查询 Builder。</param>
    /// <returns>参数合并后的 SQL。</returns>
    public string RenderSubqueryForTest(ISqlBuilder builder) => RenderSubquery(builder);

    /// <summary>
    /// 为独立 Clause 测试创建运行上下文。
    /// </summary>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="entityResolver">实体解析器。</param>
    /// <param name="aliasRegister">实体别名注册器。</param>
    /// <param name="parameterManager">参数管理器。</param>
    /// <param name="builder">SQL Builder。</param>
    /// <param name="entityMappingResolver">实体映射解析器。</param>
    /// <param name="databaseContextAccessor">数据库上下文访问器。</param>
    /// <param name="parameterFactory">SQL 参数工厂。</param>
    /// <param name="metadataOptions">SQL 元数据配置。</param>
    /// <param name="options">SQL 配置。</param>
    /// <param name="databaseContextResolver">数据库上下文解析器。</param>
    /// <returns>Clause 运行上下文。</returns>
    public static SqlClauseContext CreateTestClauseContext(IDialect dialect = null,
        IEntityResolver entityResolver = null, IEntityAliasRegister aliasRegister = null,
        IParameterManager parameterManager = null, ISqlBuilder builder = null,
        IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory parameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null)
    {
        dialect ??= TestDialect.Instance;
        var services = new SqlBuilderServices(entityMappingResolver, databaseContextAccessor, parameterFactory,
            metadataOptions, options, databaseContextResolver);
        var databaseContext = services.DatabaseContextResolver.Resolve(services.Options) ??
            services.Options.GetDatabaseContext() ?? services.DatabaseContextAccessor?.Current ??
            services.MetadataOptions.DefaultDatabaseContext;
        return new SqlClauseContext(builder ?? new TestSqlBuilder(services, dialect), new TestSqlProvider(dialect),
            entityResolver ?? new EntityResolver(), aliasRegister ?? new EntityAliasRegister(),
            parameterManager ?? new ParameterManager(dialect), new SqlBuilderExecutionContext(databaseContext), services);
    }

    /// <summary>
    /// 初始化Sql生成器
    /// </summary>
    /// <param name="dialect">Sql 方言</param>
    /// <param name="entityModelMetadataProvider">实体模型元数据提供器</param>
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
    public TestSqlBuilder(IDialect dialect = null, IEntityModelMetadataProvider entityModelMetadataProvider = null,
        IParameterManager parameterManager = null, IEntityMappingResolver entityMappingResolver = null,
        IDatabaseContextAccessor databaseContextAccessor = null, ISqlParameterFactory sqlParameterFactory = null,
        SqlMetadataOptions metadataOptions = null, SqlOptions options = null,
        ISqlDatabaseContextResolver databaseContextResolver = null,
        ISqlObjectNameFormatter objectNameFormatter = null,
        ISqlCrossDatabaseQueryValidator crossDatabaseQueryValidator = null,
        ISqlTableReferenceValidator tableReferenceValidator = null)
        : this(new SqlBuilderServices(entityMappingResolver, databaseContextAccessor, sqlParameterFactory,
            metadataOptions, options, databaseContextResolver, objectNameFormatter, crossDatabaseQueryValidator,
            tableReferenceValidator, entityModelMetadataProvider), dialect, parameterManager) { }

    /// <summary>
    /// 使用共享依赖初始化测试 SQL Builder。
    /// </summary>
    /// <param name="services">可共享服务依赖。</param>
    /// <param name="dialect">SQL 方言。</param>
    /// <param name="parameterManager">参数管理器。</param>
    internal TestSqlBuilder(SqlBuilderServices services, IDialect dialect,
        IParameterManager parameterManager = null)
        : base(new TestSqlProvider(dialect ?? TestDialect.Instance), services, parameterManager) { }

    /// <inheritdoc />
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new TestSqlBuilder(Services, Dialect, parameterManager);

    private sealed class TestSqlProvider : ISqlProvider, ISqlProviderProfileProvider
    {
        public string Key => "test.sqlserver";

        public TestSqlProvider(IDialect dialect) => Dialect = dialect;
        public DatabaseType DatabaseType => DatabaseType.SqlServer;
        public IDialect Dialect { get; }
        public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();
        public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
        public ISqlPaginationRenderer PaginationRenderer { get; } = new TestPaginationRenderer();
        public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
        public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();
        public SqlProviderProfile Profile { get; } = new()
        {
            Query = new SqlProviderQueryCapabilities
            {
                Cte = SqlQueryCapabilityState.Supported,
                Union = SqlQueryCapabilityState.Supported,
                UnionAll = SqlQueryCapabilityState.Supported,
                Intersect = SqlQueryCapabilityState.Supported,
                Except = SqlQueryCapabilityState.Supported,
                RightJoin = SqlQueryCapabilityState.Supported,
                Pagination = SqlQueryCapabilityState.Supported
            }
        };
    }

    private sealed class TestPaginationRenderer : ISqlPaginationRenderer
    {
        public string Render(string offsetParameterName, string limitParameterName) =>
            $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
    }
}
