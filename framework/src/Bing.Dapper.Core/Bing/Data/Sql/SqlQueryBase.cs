using System.Runtime.ExceptionServices;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper SQL 查询对象的可变执行状态基类。
/// </summary>
/// <remarks>
/// 实例持有可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。
/// </remarks>
public abstract partial class SqlQueryBase : ISqlQuery, ISqlPartAccessor, IGetParameter, IClearParameters, IUnionAccessor,
    ICteAccessor, ISqlQueryExecutionResourceAccessor, ISqlTransactionScopeResourceBinder, ISqlQueryMetadataBinder
{
    #region 字段

    /// <summary>
    /// Sql生成器
    /// </summary>
    private ISqlBuilder _sqlBuilder;

    /// <summary>
    /// 数据库连接
    /// </summary>
    private IDbConnection _connection;

    /// <summary>
    /// 数据库连接所有权
    /// </summary>
    private SqlResourceOwnership _connectionOwnership = SqlResourceOwnership.Owned;

    /// <summary>
    /// 事务
    /// </summary>
    private IDbTransaction _transaction;

    /// <summary>
    /// 当前事务诊断标识
    /// </summary>
    private string _transactionId;

    /// <summary>
    /// 事务作用域执行租约。
    /// </summary>
    private ISqlTransactionScopeLease _transactionScopeLease;

    /// <summary>
    /// 是否已释放事务作用域创建的子查询对象。
    /// </summary>
    private bool _isTransactionScopeChildDisposed;

    /// <summary>
    /// 是否已为主库读取创建内部事务。
    /// </summary>
    private bool _primaryReadTransactionStarted;

    /// <summary>
    /// 事务所有权
    /// </summary>
    private SqlResourceOwnership _transactionOwnership = SqlResourceOwnership.Owned;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private IParamLiteralsResolver _paramLiteralsResolver;

    /// <summary>
    /// Sql 参数绑定器
    /// </summary>
    private ISqlParameterBinder _sqlParameterBinder;

    /// <summary>
    /// 外部实体映射解析器
    /// </summary>
    private IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 外部事务解析器
    /// </summary>
    private Func<IDbTransaction> _externalTransactionResolver;

    /// <summary>
    /// 连接来源
    /// </summary>
    private SqlConnectionSource _connectionSource;

    /// <summary>
    /// 当前实例的执行租约状态。
    /// </summary>
    private int _executionLease;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="SqlQueryBase"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="options">Sql配置</param>
    protected SqlQueryBase(IServiceProvider serviceProvider, SqlOptions options)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Logger = CreateLogger();
        _connection = options.Connection;
        if (_connection != null)
            _connectionOwnership = SqlResourceOwnership.External;
        ContextId = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 创建当前查询类型使用的日志记录器。
    /// </summary>
    /// <returns>已注册日志工厂创建的记录器；未注册时返回空记录器。</returns>
    private ILogger CreateLogger()
    {
        var loggerFactory = ServiceProvider.GetService<ILoggerFactory>();
        if (loggerFactory == null)
            return NullLogger.Instance;
        return loggerFactory.CreateLogger(GetType());
    }

    #endregion

    #region 属性

    /// <summary>
    /// 上下文标识
    /// </summary>
    public string ContextId { get; private set; }

    /// <inheritdoc />
    public ISqlOutputParameterAccessor OutputParameters { get; private set; }

    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 日志操作
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Sql配置
    /// </summary>
    protected SqlOptions Options { get; set; }

    /// <summary>
    /// 实体映射解析器
    /// </summary>
    protected IEntityMappingResolver EntityMappingResolver =>
        _entityMappingResolver ?? ServiceProvider.GetService<IEntityMappingResolver>();

    /// <summary>
    /// 连接来源
    /// </summary>
    protected SqlConnectionSource ConnectionSource => _connectionSource;

    /// <summary>
    /// 当前查询解析后的数据库类型。
    /// </summary>
    /// <returns>数据库上下文中的数据源类型；上下文未解析时返回选项中的数据库类型。</returns>
    internal DatabaseType GetDatabaseType() => Options.GetDatabaseContext()?.DataSource?.DatabaseType ??
                                               Options.DatabaseType;

    /// <summary>
    /// 获取当前查询固定的数据库上下文。
    /// </summary>
    /// <returns>数据库上下文。</returns>
    internal DatabaseContext GetDatabaseContext() => Options.GetDatabaseContext();

    /// <summary>
    /// 是否启用调试SQL
    /// </summary>
    protected bool EnabledDebugSql { get; set; } = true;

    /// <summary>
    /// Sql生成器
    /// </summary>
    public ISqlBuilder SqlBuilder => _sqlBuilder ??= CreateSqlBuilder();

    /// <summary>
    /// Sql方言
    /// </summary>
    public IDialect Dialect => ((ISqlCommonPartAccessor)SqlBuilder).Dialect;

    /// <summary>
    /// 参数管理器
    /// </summary>
    public IParameterManager ParameterManager => ((ISqlCommonPartAccessor)SqlBuilder).ParameterManager;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    protected IParamLiteralsResolver ParamLiteralsResolver => _paramLiteralsResolver ??= CreateParamLiteralsResolver();

    /// <summary>
    /// Sql 参数绑定器
    /// </summary>
    protected ISqlParameterBinder SqlParameterBinder => _sqlParameterBinder ??= CreateSqlParameterBinder();

    /// <summary>
    /// Select子句
    /// </summary>
    public ISelectClause SelectClause => ((ISqlQueryClauseAccessor)SqlBuilder).SelectClause;

    /// <summary>
    /// From子句
    /// </summary>
    public IFromClause FromClause => ((ISqlQueryClauseAccessor)SqlBuilder).FromClause;

    /// <summary>
    /// Join子句
    /// </summary>
    public IJoinClause JoinClause => ((ISqlQueryClauseAccessor)SqlBuilder).JoinClause;

    /// <summary>
    /// Where子句
    /// </summary>
    public IWhereClause WhereClause => ((ISqlQueryClauseAccessor)SqlBuilder).WhereClause;

    /// <summary>
    /// GroupBy子句
    /// </summary>
    public IGroupByClause GroupByClause => ((ISqlQueryClauseAccessor)SqlBuilder).GroupByClause;

    /// <summary>
    /// OrderBy子句
    /// </summary>
    public IOrderByClause OrderByClause => ((ISqlQueryClauseAccessor)SqlBuilder).OrderByClause;

    /// <summary>
    /// 参数列表
    /// </summary>
    protected IReadOnlyDictionary<string, object> Params => SqlBuilder.GetParams();

    /// <summary>
    /// 是否包含联合操作
    /// </summary>
    public bool IsUnion => ((IUnionAccessor)SqlBuilder).IsUnion;

    /// <summary>
    /// 联合操作项集合
    /// </summary>
    public List<BuilderItem> UnionItems => ((IUnionAccessor)SqlBuilder).UnionItems;

    /// <summary>
    /// 公用表表达式CTE集合
    /// </summary>
    public List<BuilderItem> CteItems => ((ICteAccessor)SqlBuilder).CteItems;

    #endregion

    #region 工厂方法

    /// <summary>
    /// 创建绑定到当前查询状态的 SQL Builder。
    /// </summary>
    /// <returns>由当前 Provider 配置的 SQL Builder。</returns>
    protected abstract ISqlBuilder CreateSqlBuilder();

    /// <summary>
    /// 根据当前查询状态创建 SQL Builder。
    /// </summary>
    /// <param name="provider">SQL 提供程序。</param>
    /// <returns>SQL Builder。</returns>
    protected ISqlBuilder CreateSqlBuilder(ISqlProvider provider)
    {
        var factory = ServiceProvider.GetService<ISqlBuilderFactory>();
        if (factory == null)
            throw new InvalidOperationException("未注册 SQL Builder 工厂。");
        return factory.Create(provider, CreateSqlBuilderServices());
    }

    /// <summary>
    /// 创建当前查询专属的 Builder 共享服务。
    /// </summary>
    /// <remarks>
    /// 服务包可由同一 Builder 的 New 和 Clone 共享，但不得跨 Query 复用，避免泄漏 <see cref="SqlOptions"/>。
    /// </remarks>
    /// <returns>仅可在当前查询及其 Builder 派生实例间共享的服务包。</returns>
    protected virtual SqlBuilderServices CreateSqlBuilderServices() => new(
        EntityMappingResolver,
        ServiceProvider.GetService<IDatabaseContextAccessor>(),
        ServiceProvider.GetService<ISqlParameterFactory>(),
        ServiceProvider.GetService<SqlMetadataOptions>(),
        Options,
        ServiceProvider.GetService<ISqlDatabaseContextResolver>(),
        ServiceProvider.GetService<ISqlObjectNameFormatter>(),
        ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(),
        ServiceProvider.GetService<ISqlTableReferenceValidator>(),
        ServiceProvider.GetService<IEntityModelMetadataProvider>());

    /// <summary>
    /// 创建用于诊断 SQL 的参数字面值解析器。
    /// </summary>
    /// <returns>当前 Provider 适用的参数字面值解析器。</returns>
    protected virtual IParamLiteralsResolver CreateParamLiteralsResolver() => new ParamLiteralsResolver();

    /// <summary>
    /// 创建 Sql 参数绑定器
    /// </summary>
    /// <returns>Sql 参数绑定器</returns>
    protected virtual ISqlParameterBinder CreateSqlParameterBinder() =>
        ServiceProvider.GetService<ISqlParameterBinder>() ?? new DefaultSqlParameterBinder(
            EntityMappingResolver,
            ServiceProvider.GetService<IDatabaseContextAccessor>(),
            ServiceProvider.GetService<ISqlParameterFactory>(),
            ServiceProvider.GetService<SqlMetadataOptions>(),
            ServiceProvider.GetService<ISqlDatabaseContextResolver>());

    /// <summary>
    /// 解析连接字符串
    /// </summary>
    /// <returns>连接字符串</returns>
    protected virtual string ResolveConnectionString()
    {
        var contextAccessor = ServiceProvider.GetService<IDatabaseContextAccessor>();
        var metadataOptions = ServiceProvider.GetService<SqlMetadataOptions>();
        var contextResolver = ServiceProvider.GetService<ISqlDatabaseContextResolver>();
        var context = contextResolver?.Resolve(Options) ?? Options.GetDatabaseContext() ?? contextAccessor?.Current ??
                  metadataOptions?.DefaultDatabaseContext;
        var dataSource = ResolveReadPreferenceDataSource(context);
        if (string.IsNullOrWhiteSpace(dataSource?.ConnectionString) == false ||
            string.IsNullOrWhiteSpace(dataSource?.ConnectionStringName) == false)
        {
            var resolver = ServiceProvider.GetService<ISqlConnectionStringResolver>() ??
                           new DefaultSqlConnectionStringResolver(
                               ServiceProvider.GetService<ConnectionStringCollection>());
            return resolver.Resolve(dataSource);
        }
        return Options.ConnectionString;
    }

    /// <summary>
    /// 根据读取偏好解析数据源。
    /// </summary>
    /// <param name="context">数据库上下文。</param>
    /// <returns>数据源描述。</returns>
    protected virtual SqlDataSourceDescriptor ResolveReadPreferenceDataSource(DatabaseContext context)
    {
        var dataSource = context?.DataSource;
        if (context?.ReadPreference != SqlReadPreference.Primary)
            return dataSource;
        if (dataSource?.PrimaryReadStrategy != PrimaryReadStrategy.PrimaryDataSource)
            return dataSource;
        var resolver = ServiceProvider.GetService<ISqlDataSourceResolver>();
        return resolver?.Resolve(dataSource.Key, new DatabaseScopeOptions
        {
            DbKey = dataSource.Key,
            TenantId = context.TenantId,
            ReadPreference = SqlReadPreference.Primary
        }) ?? dataSource;
    }

    #endregion

    /// <summary>
    /// 获取或创建内部执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    protected IDbConnection GetExecutionConnection() =>
        ((ISqlQueryExecutionResourceAccessor)this).GetOrCreateConnection();

    /// <summary>
    /// 获取当前实例的非阻塞执行租约。
    /// </summary>
    /// <remarks>
    /// Query 和 Executor 保存可变的 Builder、连接与事务状态；同一实例只允许一个执行操作。
    /// </remarks>
    /// <returns>必须在操作结束时释放的执行租约。</returns>
    protected IDisposable AcquireExecutionLease()
    {
        if (Interlocked.CompareExchange(ref _executionLease, 1, 0) != 0)
            throw new InvalidOperationException("同一个 SQL Query 或 Executor 实例不支持并发执行，请为每个操作创建独立实例。");
        return new ExecutionLease(this);
    }

    /// <summary>
    /// 归还当前实例的执行租约。
    /// </summary>
    private void ReleaseExecutionLease() => Volatile.Write(ref _executionLease, 0);

    /// <summary>
    /// 当前实例执行租约。
    /// </summary>
    private sealed class ExecutionLease : IDisposable
    {
        /// <summary>
        /// 所属查询对象。
        /// </summary>
        private SqlQueryBase _owner;

        /// <summary>
        /// 初始化执行租约。
        /// </summary>
        /// <param name="owner">所属查询对象。</param>
        public ExecutionLease(SqlQueryBase owner) => _owner = owner;

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseExecutionLease();
        }
    }

    /// <summary>
    /// 获取或创建执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    IDbConnection ISqlQueryExecutionResourceAccessor.GetOrCreateConnection()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
        if (_connection != null)
            return _connection;
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空");
        var resolver = ServiceProvider.GetService<ISqlDbConnectionFactoryResolver>();
        if (resolver == null)
            throw new InvalidOperationException("未注册 SQL 数据库连接工厂解析器。");
        _connection = resolver.Create(GetDatabaseType(), connectionString);
        _connectionOwnership = SqlResourceOwnership.Owned;
        if (_connection == null)
            throw new InvalidOperationException("数据库连接不能为空");
        return _connection;
    }

    #region Config(配置)

    /// <summary>
    /// 配置
    /// </summary>
    /// <param name="configAction">配置操作</param>
    public void Config(Action<SqlOptions> configAction) => configAction?.Invoke(Options);

    /// <summary>
    /// 绑定执行连接。
    /// </summary>
    /// <param name="connection">数据库连接。</param>
    /// <param name="ownership">连接所有权。</param>
    /// <param name="source">连接来源。</param>
    private void BindConnection(IDbConnection connection, SqlResourceOwnership ownership, SqlConnectionSource source)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        EnsureConnectionCanBeReplaced(connection);
        ValidateExternalConnectionDatabaseIdentity(connection);
        if (_connection != null && ReferenceEquals(_connection, connection))
        {
            _connectionOwnership = ownership;
            _connectionSource = source;
            return;
        }
        if (_connection != null && _connectionOwnership == SqlResourceOwnership.External)
            throw new InvalidOperationException("当前 Query 已绑定其他外部连接，不能静默替换连接资源。");
        ReleaseOwnedConnectionBeforeReplacement();
        _connection = connection;
        _connectionOwnership = ownership;
        _connectionSource = source;
    }

    /// <summary>
    /// 确保当前连接可以被替换。
    /// </summary>
    /// <param name="connection">待绑定连接。</param>
    private void EnsureConnectionCanBeReplaced(IDbConnection connection)
    {
        if (_transaction == null)
            return;
        if (ReferenceEquals(_transaction.Connection, connection))
            return;
        throw new InvalidOperationException("当前 Query 存在活动事务，不能替换数据库连接。");
    }

    /// <summary>
    /// 在替换连接前释放原有自有连接。
    /// </summary>
    private void ReleaseOwnedConnectionBeforeReplacement()
    {
        if (_connection == null || _connectionOwnership != SqlResourceOwnership.Owned)
            return;
        var connection = _connection;
        Exception closeException = null;
        try
        {
            if (connection.State != ConnectionState.Closed)
                connection.Close();
        }
        catch (Exception exception)
        {
            closeException = exception;
        }
        try
        {
            connection.Dispose();
        }
        catch (Exception exception)
        {
            if (closeException != null)
                throw new AggregateException(closeException, exception);
            throw;
        }
        if (closeException != null)
            ExceptionDispatchInfo.Capture(closeException).Throw();
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
        _connectionSource = SqlConnectionSource.Unknown;
    }

    /// <summary>
    /// 一次性绑定外部实体映射解析器。
    /// </summary>
    /// <param name="resolver">实体映射解析器。</param>
    private void BindEntityMappingResolver(IEntityMappingResolver resolver)
    {
        _entityMappingResolver = resolver;
    }

    /// <summary>
    /// 绑定实体映射解析器。
    /// </summary>
    /// <param name="resolver">实体映射解析器。</param>
    void ISqlQueryMetadataBinder.BindEntityMappingResolver(IEntityMappingResolver resolver) =>
        BindEntityMappingResolver(resolver);

    /// <summary>
    /// 校验外部连接与当前固定数据库上下文的物理身份。
    /// </summary>
    /// <param name="connection">外部数据库连接。</param>
    private void ValidateExternalConnectionDatabaseIdentity(IDbConnection connection)
    {
        var context = Options.GetDatabaseContext();
        var dataSource = context?.DataSource;
        if (dataSource == null ||
            (string.IsNullOrWhiteSpace(dataSource.ConnectionString) &&
             string.IsNullOrWhiteSpace(dataSource.ConnectionStringName)))
            return;
        if (string.IsNullOrWhiteSpace(connection.ConnectionString))
            throw new InvalidOperationException("外部连接缺少用于校验数据库身份的连接字符串。");
        var resolver = ServiceProvider.GetService<ISqlDatabaseIdentityResolver>();
        if (resolver == null)
            throw new InvalidOperationException("未注册 SQL 数据库身份解析器，无法校验外部连接。");
        var connectionStringResolver = ServiceProvider.GetService<ISqlConnectionStringResolver>() ??
                                       new DefaultSqlConnectionStringResolver(
                                           ServiceProvider.GetService<ConnectionStringCollection>());
        var expectedConnectionString = connectionStringResolver.Resolve(dataSource);
        var databaseType = dataSource.DatabaseType == default ? Options.DatabaseType : dataSource.DatabaseType;
        var queryIdentity = resolver.Resolve(databaseType, expectedConnectionString);
        var externalIdentity = resolver.Resolve(databaseType, connection.ConnectionString);
        if (queryIdentity.IsComparable && externalIdentity.IsComparable && queryIdentity.Equals(externalIdentity))
            return;
        throw new InvalidOperationException(
            $"外部连接数据库身份与 Query 上下文不一致。Query DbKey={context?.DbKey ?? "<default>"}; " +
            $"Query DatabaseType={databaseType}; ExternalIdentity={FormatDatabaseIdentity(externalIdentity)}; " +
            $"QueryIdentity={FormatDatabaseIdentity(queryIdentity)}。");
    }

    /// <summary>
    /// 格式化不含凭据的数据库物理身份。
    /// </summary>
    /// <param name="identity">数据库物理身份。</param>
    /// <returns>诊断使用的脱敏身份文本。</returns>
    private static string FormatDatabaseIdentity(SqlDatabaseIdentity identity)
    {
        if (identity == null)
            return "<null>";
        return $"Type={identity.DatabaseType};Server={identity.Server};Port={identity.Port};Database={identity.Database};" +
               $"FilePath={identity.FilePath};ServiceName={identity.ServiceName};Sid={identity.Sid};Alias={identity.OracleAlias}";
    }

    #endregion

    /// <summary>
    /// 在执行之后清空Sql和参数
    /// </summary>
    protected void ClearAfterExecution()
    {
        EnabledDebugSql = true;
        if (Options.IsClearAfterExecution == false)
            return;
        SqlBuilder.Clear();
    }

    /// <summary>
    /// 获取调试Sql语句
    /// </summary>
    public string GetDebugSql() => SqlBuilder.ToDebugSql();

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    protected string GetSql() => SqlBuilder.ToSql();

    /// <summary>
    /// 获取Sql生成器
    /// </summary>
    public ISqlBuilder GetBuilder() => SqlBuilder;

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters() => GetDbParameters(SqlBuilder);

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters(ISqlBuilder builder)
        => GetDbParameters(builder, null);

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <param name="builder">Sql 生成器</param>
    /// <param name="sql">当前执行的 Sql 语句</param>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters(ISqlBuilder builder, string sql)
    {
        var parameters = SqlParameterBinder is ISqlParameterContextBinder binder
            ? binder.Bind(builder, Options, CreateParameterBindingContext(sql, builder?.GetParams()))
            : SqlParameterBinder.Bind(builder);
        OutputParameters = parameters as ISqlOutputParameterAccessor;
        return parameters;
    }

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <param name="parameter">原始参数对象</param>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters(object parameter)
        => GetDbParameters(parameter, null);

    /// <summary>
    /// 获取数据库参数
    /// </summary>
    /// <param name="parameter">原始参数对象</param>
    /// <param name="sql">当前执行的 Sql 语句</param>
    /// <returns>数据库参数</returns>
    protected object GetDbParameters(object parameter, string sql)
    {
        var parameters = SqlParameterBinder is ISqlParameterContextBinder binder
            ? binder.Bind(parameter, Options, CreateParameterBindingContext(sql, parameter))
            : SqlParameterBinder.Bind(parameter);
        OutputParameters = parameters as ISqlOutputParameterAccessor;
        return parameters;
    }

    /// <summary>
    /// 创建参数绑定上下文
    /// </summary>
    /// <param name="sql">当前执行的 Sql 语句</param>
    /// <param name="source">原始参数源</param>
    /// <param name="entityType">关联实体类型</param>
    /// <returns>参数绑定上下文</returns>
    protected virtual SqlParameterBindingContext CreateParameterBindingContext(string sql, object source,
        Type entityType = null)
    {
        var context = Options.GetDatabaseContext();
        return new SqlParameterBindingContext
        {
            Sql = sql,
            DbKey = context?.DataSource?.Key ?? context?.DbKey,
            DatabaseType = context?.DataSource?.DatabaseType ?? Options.DatabaseType,
            EntityType = entityType,
            Source = source
        };
    }

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">获取列表操作</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    public virtual PagerList<TResult> PagerQuery<TResult>(Func<List<TResult>> func, IPager parameter, int? timeout = null)
    {
        parameter = GetPage(parameter);
        if (parameter.TotalCount == 0)
            parameter.TotalCount = GetCount(timeout);
        SetPager(parameter);
        return new PagerList<TResult>(parameter, func());
    }

    /// <summary>
    /// 获取行数
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    protected int GetCount(int? timeout = null)
    {
        using var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        try
        {
            var builder = GetCountBuilder();
            var sql = builder.ToSql();
            var conn = GetExecutionConnection();
            var dbParameters = GetDbParameters(builder);
            var parameterMetadata = GetSqlParameterDiagnostics(builder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, builder.GetParams(), conn, parameterMetadata);

            WriteTraceLog(builder, sql);
            var result = conn.ExecuteScalar(sql, dbParameters, transaction, timeout);

            CompleteQueryTransaction();
            ExecuteAfter(message);
            return Conv.ToInt(result);
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
    }

    /// <summary>
    /// 设置分页参数
    /// </summary>
    /// <param name="parameter">分页参数</param>
    private void SetPager(IPager parameter)
    {
        SqlBuilder.OrderBy(parameter.Order);
        SqlBuilder.Page(parameter);
    }

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <typeparam name="TResult">返回结果类型</typeparam>
    /// <param name="func">不接收取消令牌的获取列表操作。</param>
    /// <param name="parameter">分页参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <remarks>该重载无法将取消令牌传递给列表操作，请改用接收 <see cref="CancellationToken"/> 的重载。</remarks>
    [Obsolete("请使用接收 CancellationToken 的 PagerQueryAsync 重载")]
    public virtual Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<Task<List<TResult>>> func, IPager parameter,
        int? timeout = null)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        return PagerQueryAsync(_ => func(), parameter, timeout);
    }

    /// <summary>
    /// 分页查询。
    /// </summary>
    /// <typeparam name="TResult">返回结果类型。</typeparam>
    /// <param name="func">使用取消令牌获取列表的操作。</param>
    /// <param name="parameter">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终分页结果的异步操作。</returns>
    public virtual async Task<PagerList<TResult>> PagerQueryAsync<TResult>(Func<CancellationToken, Task<List<TResult>>> func,
        IPager parameter, int? timeout = null, CancellationToken cancellationToken = default)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));
        cancellationToken.ThrowIfCancellationRequested();
        parameter = GetPage(parameter);
        if (parameter.TotalCount == 0)
            parameter.TotalCount = await GetCountAsync(timeout, cancellationToken);
        SetPager(parameter);
        return new PagerList<TResult>(parameter, await func(cancellationToken));
    }

    /// <summary>
    /// 临时禁用调试日志
    /// </summary>
    public ISqlQuery DisableDebugLog()
    {
        EnabledDebugSql = false;
        return this;
    }

    /// <summary>
    /// 获取行数
    /// </summary>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    protected async Task<int> GetCountAsync(int? timeout = null, CancellationToken cancellationToken = default)
    {
        using var executionLease = AcquireExecutionLease();
        DiagnosticsMessage message = null;
        try
        {
            var builder = GetCountBuilder();
            var sql = builder.ToSql();
            var conn = GetExecutionConnection();
            var dbParameters = GetDbParameters(builder);
            var parameterMetadata = GetSqlParameterDiagnostics(builder);
            var transaction = GetQueryTransaction();
            message = ExecuteBefore(sql, builder.GetParams(), conn, parameterMetadata);

            WriteTraceLog(builder, sql);
            var result = await conn.ExecuteScalarAsync(new CommandDefinition(sql, dbParameters, transaction, timeout,
                cancellationToken: cancellationToken));

            CompleteQueryTransaction();
            ExecuteAfter(message);
            return Conv.ToInt(result);
        }
        catch (Exception e)
        {
            RollbackQueryTransaction();
            ExecuteError(message, e);
            throw;
        }
    }

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="debugSql">调试Sql语句</param>
    protected virtual void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        if (EnabledDebugSql == false)
            return;
        var message = new StringBuilder();
        foreach (var param in parameters)
            message.AppendLine($"    {param.Key} : {ParamLiteralsResolver.GetParamLiterals(param.Value)} : {param.Value?.GetType()},");
        var result = message.ToString().RemoveEnd($",{Common.Line}");
        Logger.LogTrace("原始Sql:\r\n{Sql}\r\n调试Sql:\r\n{DebugSql}\r\nSql参数:\r\n{SqlParam}\r\n", sql, debugSql, result);
    }

    /// <summary>
    /// 写查询跟踪日志。
    /// </summary>
    /// <param name="builder">Sql生成器。</param>
    /// <param name="sql">已生成的Sql语句。</param>
    private void WriteTraceLog(ISqlBuilder builder, string sql)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false || EnabledDebugSql == false)
            return;
        WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql(sql));
    }

    /// <summary>
    /// 获取分页参数
    /// </summary>
    /// <param name="parameter">分页参数</param>
    protected IPager GetPage(IPager parameter)
    {
        if (parameter != null)
            return parameter;
        return SqlBuilder.Pager;
    }

    /// <summary>
    /// 获取行数Sql生成器
    /// </summary>
    protected ISqlBuilder GetCountBuilder()
    {
        var builder = SqlBuilder.Clone();
        ClearCountBuilder(builder);
        if (IsUnion)
            return GetCountBuilderByUnion(builder);
        if (IsGroup(builder))
            return GetCountBuilderByGroup(builder);
        return GetCountBuilder(builder);
    }

    /// <summary>
    /// 清空行数Sql生成器
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    private void ClearCountBuilder(ISqlBuilder builder)
    {
        builder.ClearOrderBy();
        builder.ClearPageParams();
    }

    /// <summary>
    /// 获取行数Sql生成器 - 联合
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilderByUnion(ISqlBuilder countBuilder) => countBuilder.New().Count().From(countBuilder, "t");

    /// <summary>
    /// 是否分组
    /// </summary>
    /// <param name="builder">Sql生成器</param>
    private bool IsGroup(ISqlBuilder builder)
    {
        if (builder is ISqlQueryClauseAccessor accessor)
            return accessor.GroupByClause.IsGroup;
        return false;
    }

    /// <summary>
    /// 获取行数Sql生成器 - 分组
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilderByGroup(ISqlBuilder countBuilder)
    {
        countBuilder.ClearSelect();
        return countBuilder.New().Count().From(countBuilder.AppendSelect("1 As c"), "t");
    }

    /// <summary>
    /// 获取行数Sql生成器
    /// </summary>
    /// <param name="countBuilder">行数Sql生成器</param>
    private ISqlBuilder GetCountBuilder(ISqlBuilder countBuilder)
    {
        countBuilder.ClearSelect();
        return countBuilder.Count();
    }

    #region Dispose(释放资源)

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        try
        {
            ReleaseTransaction();
            ReleaseConnection();
        }
        finally
        {
            if (_transactionScopeLease != null)
                _isTransactionScopeChildDisposed = true;
        }
    }

    /// <summary>
    /// 确保未继续使用已释放的事务作用域子对象。
    /// </summary>
    private void ThrowIfTransactionScopeChildDisposed()
    {
        if (_isTransactionScopeChildDisposed)
            throw new ObjectDisposedException(nameof(SqlQueryBase), "事务作用域创建的 Query 或 Executor 已释放，不能继续使用。");
    }

    /// <summary>
    /// 释放事务
    /// </summary>
    private void ReleaseTransaction()
    {
        if (_transactionOwnership == SqlResourceOwnership.Owned)
            _transaction?.Dispose();
        _transaction = null;
        _transactionId = null;
        _transactionOwnership = SqlResourceOwnership.Owned;
    }

    /// <summary>
    /// 释放连接
    /// </summary>
    private void ReleaseConnection()
    {
        if (_connectionOwnership == SqlResourceOwnership.Owned)
            _connection?.Dispose();
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
    }

    /// <summary>
    /// 关闭并释放内部拥有的连接
    /// </summary>
    private void CloseOwnedConnection()
    {
        if (_connectionOwnership != SqlResourceOwnership.Owned)
            return;
        if (_connection?.State == ConnectionState.Open)
            _connection.Close();
    }

    #endregion

    /// <summary>
    /// 获取Sql参数值
    /// </summary>
    /// <typeparam name="T">参数值类型</typeparam>
    /// <param name="name">参数名</param>
    public virtual T GetParam<T>(string name)
    {
        return (T)ParameterManager?.GetValue(name);
    }

    /// <summary>
    /// 清空Sql参数
    /// </summary>
    public void ClearParams()
    {
        ParameterManager?.Clear();
    }

    /// <summary>
    /// 清理
    /// </summary>
    protected void Clear()
    {
        ClearAfterExecution();
        ClearParams();
    }

    /// <summary>
    /// 获取存储过程名城管
    /// </summary>
    /// <param name="procedure">存储过程</param>
    protected virtual string GetProcedure(string procedure) => procedure;

    /// <summary>
    /// 获取存储过程命令类型
    /// </summary>
    protected virtual CommandType GetProcedureCommandType() => CommandType.StoredProcedure;
}
