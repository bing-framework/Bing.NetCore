using System.Runtime.ExceptionServices;
using Bing.Data.Sql.Builders;
using System.Text.RegularExpressions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Bing.Data.Sql;

/// <summary>
/// Dapper SQL 查询对象的可变执行状态基类。
/// </summary>
/// <remarks>
/// 实例持有可变的 Sql 生成器、连接和事务状态，不能被多个并发操作共享。每个独立操作应使用独立实例。
/// </remarks>
public abstract partial class SqlQueryBase : ISqlQuery, ISqlQueryPlanExecutor
{
    #region 字段

    /// <summary>
    /// Sql生成器
    /// </summary>
    private ISqlBuilder _sqlBuilder;

    /// <summary>
    /// 当前查询首次解析后固定使用的 SQL Provider。
    /// </summary>
    private ISqlProvider _provider;

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

    /// <summary>
    /// 当前 Root Query 是否已释放。
    /// </summary>
    private int _isDisposed;

    /// <summary>
    /// 当前是否正在执行独立查询描述。
    /// </summary>
    /// <remarks>
    /// 独立查询描述复用 Root Query 的连接和诊断生命周期，但不能清空 Root Builder。
    /// </remarks>
    private int _queryPlanExecutionDepth;

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
        SqlQueryRuntimeBridge.Register(this, new RuntimeController(this));
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
    /// 获取当前查询和实体 Mutation 共享的 SQL Provider。
    /// </summary>
    /// <remarks>
    /// 查询 Builder 首次创建时会固定 Provider 实例；后续实体 Mutation 必须复用该实例，
    /// 避免同一 <see cref="DatabaseType"/> 下的多个 Provider 因兼容映射而发生分叉。
    /// </remarks>
    /// <returns>当前执行上下文解析出的 SQL Provider。</returns>
    protected ISqlProvider GetCurrentProvider()
    {
        if (_provider != null)
            return _provider;
        var providerResolver = ServiceProvider.GetService<ISqlProviderResolver>();
        if (providerResolver == null)
            throw new InvalidOperationException("未注册 SQL Provider 解析器。");
        return _provider = providerResolver.Resolve(GetDatabaseContext(), databaseType: GetDatabaseType());
    }

    /// <summary>
    /// 是否启用调试SQL
    /// </summary>
    protected bool EnabledDebugSql { get; set; } = true;

    /// <summary>
    /// Sql生成器
    /// </summary>
    protected ISqlBuilder SqlBuilder => _sqlBuilder ??= CreateSqlBuilder();

    /// <summary>
    /// Sql方言
    /// </summary>
    protected IDialect Dialect => ((ISqlCommonPartAccessor)SqlBuilder).Dialect;

    /// <summary>
    /// 参数管理器
    /// </summary>
    protected IParameterManager ParameterManager => ((ISqlCommonPartAccessor)SqlBuilder).ParameterManager;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    protected IParamLiteralsResolver ParamLiteralsResolver => _paramLiteralsResolver ??= CreateParamLiteralsResolver();

    /// <summary>
    /// Sql 参数绑定器
    /// </summary>
    private protected ISqlParameterBinder SqlParameterBinder => _sqlParameterBinder ??= CreateSqlParameterBinder();

    /// <summary>
    /// Select子句
    /// </summary>
    protected ISelectClause SelectClause => ((ISqlQueryClauseAccessor)SqlBuilder).SelectClause;

    /// <summary>
    /// From子句
    /// </summary>
    protected IFromClause FromClause => ((ISqlQueryClauseAccessor)SqlBuilder).FromClause;

    /// <summary>
    /// Join子句
    /// </summary>
    protected IJoinClause JoinClause => ((ISqlQueryClauseAccessor)SqlBuilder).JoinClause;

    /// <summary>
    /// Where子句
    /// </summary>
    protected IWhereClause WhereClause => ((ISqlQueryClauseAccessor)SqlBuilder).WhereClause;

    /// <summary>
    /// GroupBy子句
    /// </summary>
    protected IGroupByClause GroupByClause => ((ISqlQueryClauseAccessor)SqlBuilder).GroupByClause;

    /// <summary>
    /// OrderBy子句
    /// </summary>
    protected IOrderByClause OrderByClause => ((ISqlQueryClauseAccessor)SqlBuilder).OrderByClause;

    /// <summary>
    /// 参数列表
    /// </summary>
    protected IReadOnlyDictionary<string, object> Params => SqlBuilder.GetParams();

    /// <summary>
    /// 是否包含联合操作
    /// </summary>
    protected bool IsUnion => ((IUnionAccessor)SqlBuilder).IsUnion;

    /// <summary>
    /// 联合操作项集合
    /// </summary>
    protected List<BuilderItem> UnionItems => ((IUnionAccessor)SqlBuilder).UnionItems;

    /// <summary>
    /// 公用表表达式CTE集合
    /// </summary>
    protected List<BuilderItem> CteItems => ((ICteAccessor)SqlBuilder).CteItems;

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
        var providerResolver = ServiceProvider.GetService<ISqlProviderResolver>();
        if (providerResolver == null)
            throw new InvalidOperationException("未注册 SQL Provider 解析器。");
        var resolvedProvider = providerResolver.Resolve(GetDatabaseContext(), provider, GetDatabaseType());
        _provider = resolvedProvider;
        return factory.Create(resolvedProvider, CreateSqlBuilderServices());
    }

    /// <summary>
    /// 创建供独立查询描述使用的 SQL Builder。
    /// </summary>
    /// <remarks>
    /// 每次调用必须返回新的 Builder，避免不同查询描述之间共享可变状态。
    /// </remarks>
    /// <returns>绑定当前 Provider 的独立 SQL Builder。</returns>
    protected virtual ISqlBuilder CreateIndependentSqlBuilder() => CreateSqlBuilder(GetCurrentProvider());

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderSource.CreateIndependentSqlBuilder() => CreateIndependentSqlBuilder();

    /// <inheritdoc />
    public SqlQuery<TResult> Sql<TResult>()
    {
        EnsureExecutionAvailable();
        var executor = (ISqlQueryPlanExecutor)this;
        return new SqlQuery<TResult>(executor, executor.CreateIndependentSqlBuilder());
    }

    /// <inheritdoc />
    public SqlTextQuery<TResult> Sql<TResult>(string sql, object parameters = null)
    {
        EnsureExecutionAvailable();
        return new SqlTextQuery<TResult>((ISqlQueryPlanExecutor)this, sql, parameters);
    }

    /// <inheritdoc />
    public SqlTextQuery<TResult> SqlInterpolated<TResult>(FormattableString sql)
    {
        EnsureExecutionAvailable();
        if (sql == null)
            throw new ArgumentNullException(nameof(sql));

        var parameterPrefix = GetCurrentProvider().Dialect.GetPrefix();
        var arguments = sql.GetArguments();
        var parameters = new Dictionary<string, object>(arguments.Length);
        var parameterNames = new Dictionary<int, string>(arguments.Length);
        for (var index = 0; index < arguments.Length; index++)
        {
            var parameterName = GetInterpolatedParameterName(sql.Format, index, parameterPrefix);
            parameterNames.Add(index, parameterName);
            parameters.Add(parameterName, arguments[index]);
        }
        var commandText = CreateInterpolatedCommandText(sql.Format, parameterNames, parameterPrefix);
        return Sql<TResult>(commandText, parameters);
    }

    /// <inheritdoc />
    public SqlProcedureQuery<TResult> Procedure<TResult>(string procedure, object parameters = null)
    {
        EnsureExecutionAvailable();
        if (string.IsNullOrWhiteSpace(procedure))
            throw new ArgumentException("存储过程名称不能为空。", nameof(procedure));
        var executor = (ISqlQueryPlanExecutor)this;
        return new SqlProcedureQuery<TResult>(executor, GetProcedure(procedure), parameters);
    }

    /// <summary>
    /// 将复合格式 SQL 转换为仅包含参数占位符的命令文本。
    /// </summary>
    /// <param name="format">插值字符串的复合格式文本。</param>
    /// <param name="parameterNames">格式项索引对应的参数名称。</param>
    /// <param name="parameterPrefix">当前 SQL 方言使用的参数前缀。</param>
    /// <returns>可交由 Dapper 执行的参数化 SQL 文本。</returns>
    /// <remarks>
    /// 插值值始终交由参数绑定器处理，因此格式项的对齐和格式说明仅用于验证复合格式语法，不会影响 SQL 文本。
    /// </remarks>
    /// <exception cref="FormatException">复合格式文本包含无效格式项时抛出。</exception>
    private static string CreateInterpolatedCommandText(string format, IReadOnlyDictionary<int, string> parameterNames,
        string parameterPrefix)
    {
        if (format == null)
            throw new ArgumentNullException(nameof(format));
        if (parameterNames == null)
            throw new ArgumentNullException(nameof(parameterNames));
        if (string.IsNullOrWhiteSpace(parameterPrefix))
            throw new ArgumentException("SQL 方言参数前缀不能为空。", nameof(parameterPrefix));
        var result = new StringBuilder(format.Length + parameterNames.Count * 4);
        for (var position = 0; position < format.Length; position++)
        {
            var current = format[position];
            if (current == '{')
            {
                if (position + 1 < format.Length && format[position + 1] == '{')
                {
                    result.Append('{');
                    position++;
                    continue;
                }
                var indexStart = ++position;
                while (position < format.Length && char.IsDigit(format[position]))
                    position++;
                if (indexStart == position || int.TryParse(format.Substring(indexStart, position - indexStart), out var index) == false ||
                    parameterNames.TryGetValue(index, out var parameterName) == false)
                    throw new FormatException("插值 SQL 包含无效的格式项索引。");
                SkipInterpolatedFormatItem(format, ref position);
                result.Append(parameterPrefix).Append(parameterName);
                continue;
            }
            if (current == '}')
            {
                if (position + 1 >= format.Length || format[position + 1] != '}')
                    throw new FormatException("插值 SQL 包含未转义的右花括号。");
                result.Append('}');
                position++;
                continue;
            }
            result.Append(current);
        }
        return result.ToString();
    }

    /// <summary>
    /// 跳过复合格式项的可选对齐和格式说明，并定位到右花括号后的下一个字符。
    /// </summary>
    /// <param name="format">完整复合格式文本。</param>
    /// <param name="position">当前格式项索引后的读取位置。</param>
    /// <exception cref="FormatException">格式项未以右花括号结束时抛出。</exception>
    private static void SkipInterpolatedFormatItem(string format, ref int position)
    {
        if (position < format.Length && format[position] == ',')
        {
            position++;
            while (position < format.Length && char.IsWhiteSpace(format[position]))
                position++;
            if (position < format.Length && (format[position] == '+' || format[position] == '-'))
                position++;
            var alignmentStart = position;
            while (position < format.Length && char.IsDigit(format[position]))
                position++;
            if (alignmentStart == position)
                throw new FormatException("插值 SQL 格式项的对齐宽度无效。");
        }
        if (position < format.Length && format[position] == ':')
        {
            position++;
            while (position < format.Length && format[position] != '}')
            {
                if (format[position] is '{' or '\r' or '\n')
                    throw new FormatException("插值 SQL 格式项包含无效字符。");
                position++;
            }
        }
        if (position >= format.Length || format[position] != '}')
            throw new FormatException("插值 SQL 格式项缺少右花括号。");
    }

    /// <summary>
    /// 获取不与 SQL 文本中已有参数冲突的插值参数名。
    /// </summary>
    /// <param name="format">复合格式 SQL 文本。</param>
    /// <param name="index">插值参数索引。</param>
    /// <param name="parameterPrefix">当前 SQL 方言使用的参数前缀。</param>
    /// <returns>当前插值参数使用的名称。</returns>
    private static string GetInterpolatedParameterName(string format, int index, string parameterPrefix)
    {
        if (string.IsNullOrWhiteSpace(parameterPrefix))
            throw new ArgumentException("SQL 方言参数前缀不能为空。", nameof(parameterPrefix));
        var baseName = $"p{index}";
        var parameterName = baseName;
        var suffix = 0;
        while (Regex.IsMatch(format, $@"{Regex.Escape(parameterPrefix)}{Regex.Escape(parameterName)}(?![A-Za-z0-9_])",
                   RegexOptions.IgnoreCase))
            parameterName = $"{baseName}_{++suffix}";
        return parameterName;
    }

    /// <inheritdoc />
    public SqlLambdaQuery<TEntity> Lambda<TEntity>() where TEntity : class
    {
        EnsureExecutionAvailable();
        var executor = (ISqlQueryPlanExecutor)this;
        var query = new SqlLambdaQuery<TEntity>(executor, executor.CreateIndependentSqlBuilder());
        query.Select().From();
        return query;
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
    private protected virtual ISqlParameterBinder CreateSqlParameterBinder() =>
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
        GetOrCreateConnection();

    /// <summary>
    /// 获取当前实例的非阻塞执行租约。
    /// </summary>
    /// <remarks>
    /// Query 和 Executor 保存可变的 Builder、连接与事务状态；同一实例只允许一个执行操作。
    /// </remarks>
    /// <returns>必须在操作结束时释放的执行租约。</returns>
    protected IDisposable AcquireExecutionLease()
    {
        EnsureExecutionAvailable();
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
    /// 查询专属运行时资源控制器。
    /// </summary>
    private sealed class RuntimeController : ISqlQueryRuntimeController
    {
        private readonly SqlQueryBase _owner;

        public RuntimeController(SqlQueryBase owner) => _owner = owner;

        public IDbConnection GetOrCreateConnection() => _owner.GetOrCreateConnection();

        public IDbTransaction GetCurrentTransaction() => _owner.GetCurrentTransaction();

        public string GetCurrentTransactionId() => _owner.GetCurrentTransactionId();

        public void BindOwnedConnection(IDbConnection connection, SqlConnectionSource source) =>
            _owner.BindConnection(connection, SqlResourceOwnership.Owned, source);

        public void BindExternalConnection(IDbConnection connection, SqlConnectionSource source) =>
            _owner.BindConnection(connection, SqlResourceOwnership.External, source);

        public void BindExternalTransaction(IDbTransaction transaction, string transactionId = null) =>
            _owner.BindExternalTransaction(transaction, transactionId);

        public void BindExternalTransactionResolver(Func<IDbTransaction> resolver) =>
            _owner.BindExternalTransactionResolver(resolver);

        public void BindTransactionScope(DatabaseContext context, IDbConnection connection, IDbTransaction transaction,
            ISqlTransactionScopeLease lease) => _owner.SetTransactionContext(context, connection, transaction, lease);

        public void BindEntityMappingResolver(IEntityMappingResolver resolver) =>
            _owner.BindEntityMappingResolver(resolver);
    }

    /// <summary>
    /// 获取或创建执行连接。
    /// </summary>
    /// <returns>执行使用的数据库连接。</returns>
    private IDbConnection GetOrCreateConnection()
    {
        EnsureExecutionAvailable();
        if (_connection != null)
            return _connection;
        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("数据库连接字符串不能为空");
        var resolver = ServiceProvider.GetService<ISqlDbConnectionFactoryResolver>();
        if (resolver == null)
            throw new InvalidOperationException("未注册 SQL 数据库连接工厂解析器。");
        _connection = resolver.Create(GetCurrentProvider().Key, connectionString);
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
        if (Options.IsClearAfterExecution == false || _sqlBuilder == null)
            return;
        _sqlBuilder.Clear();
    }

    /// <summary>
    /// 获取Sql语句
    /// </summary>
    protected string GetSql() => SqlBuilder.ToSql();

    /// <summary>
    /// 验证普通查询 API 不会执行缺少 Returning 的 Mutation。
    /// </summary>
    protected void ValidateQueryBuilder()
    {
        ValidateQueryBuilder(_sqlBuilder);
    }

    /// <summary>
    /// 验证指定 Builder 可通过查询结果 API 执行。
    /// </summary>
    /// <param name="builder">待验证的 SQL Builder；原生 SQL 文本查询传入 null。</param>
    protected void ValidateQueryBuilder(ISqlBuilder builder)
    {
        if (builder == null)
            return;
        if (builder.OperationKind is not (SqlOperationKind.InsertValues or SqlOperationKind.InsertSelect or
            SqlOperationKind.Update or SqlOperationKind.Delete))
            return;
        if (builder is IReturningClauseAccessor { ReturningClause.IsEmpty: false })
            return;
        throw new InvalidOperationException("Mutation 必须配置 Returning 后才能通过查询结果 API 执行。");
    }

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
    /// 临时禁用调试日志
    /// </summary>
    public ISqlQuery DisableDebugLog()
    {
        EnabledDebugSql = false;
        return this;
    }

    /// <summary>
    /// 为独立查询计划创建临时调试日志范围。
    /// </summary>
    /// <remarks>
    /// <see cref="DisableDebugLog"/> 只影响紧随其后的一个查询计划；范围结束后必须恢复默认调试状态，
    /// 包括执行失败、取消和流式枚举提前终止。
    /// </remarks>
    /// <param name="consumeTemporaryState">是否在当前范围结束时消费并恢复临时禁用状态。</param>
    /// <returns>在范围结束时按配置恢复临时调试状态的对象。</returns>
    private IDisposable BeginQueryPlanDebugLogScope(bool consumeTemporaryState = true) =>
        new QueryPlanDebugLogScope(this, consumeTemporaryState && EnabledDebugSql == false);

    /// <summary>
    /// 独立查询计划的临时调试日志范围。
    /// </summary>
    private sealed class QueryPlanDebugLogScope : IDisposable
    {
        /// <summary>
        /// 所属 Root 查询。
        /// </summary>
        private SqlQueryBase _owner;

        /// <summary>
        /// 是否需要在范围结束时恢复调试状态。
        /// </summary>
        private readonly bool _restoreDebugLog;

        /// <summary>
        /// 初始化一个<see cref="QueryPlanDebugLogScope"/>类型的实例。
        /// </summary>
        /// <param name="owner">所属 Root 查询。</param>
        /// <param name="restoreDebugLog">是否需要恢复调试状态。</param>
        public QueryPlanDebugLogScope(SqlQueryBase owner, bool restoreDebugLog)
        {
            _owner = owner;
            _restoreDebugLog = restoreDebugLog;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner != null && _restoreDebugLog)
                owner.EnabledDebugSql = true;
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
        {
            var isSensitive = IsSensitiveParameter(param.Key);
            var literal = isSensitive ? "'<redacted>'" : ParamLiteralsResolver.GetParamLiterals(param.Value);
            var type = isSensitive ? "<redacted>" : param.Value?.GetType().ToString();
            message.AppendLine($"    {param.Key} : {literal} : {type},");
        }
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

    #region Dispose(释放资源)

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;
        Exception transactionException = null;
        try
        {
            ReleaseTransaction();
        }
        catch (Exception exception)
        {
            transactionException = exception;
        }
        try
        {
            ReleaseConnection();
        }
        catch (Exception connectionException)
        {
            if (transactionException != null)
                throw new AggregateException(transactionException, connectionException);
            throw;
        }
        finally
        {
            if (_transactionScopeLease != null)
                _isTransactionScopeChildDisposed = true;
            SqlQueryRuntimeBridge.Remove(this);
        }
        if (transactionException != null)
            ExceptionDispatchInfo.Capture(transactionException).Throw();
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
    /// 确保当前查询可以在其所属事务作用域中继续执行。
    /// </summary>
    /// <remarks>
    /// 事务作用域结束应优先于对象释放状态报告，以便调用方识别跨作用域复用错误。
    /// </remarks>
    private void EnsureExecutionAvailable()
    {
        _transactionScopeLease?.EnsureActive();
        ThrowIfTransactionScopeChildDisposed();
        ThrowIfDisposed();
    }

    /// <summary>
    /// 确保 Root Query 未被释放。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            throw new ObjectDisposedException(nameof(SqlQueryBase), "SQL Query 或 Executor 已释放，不能继续执行或获取资源。");
    }

    /// <summary>
    /// 释放事务
    /// </summary>
    private void ReleaseTransaction()
    {
        var transaction = _transaction;
        var ownership = _transactionOwnership;
        _transaction = null;
        _transactionId = null;
        _transactionOwnership = SqlResourceOwnership.Owned;
        if (ownership == SqlResourceOwnership.Owned)
            transaction?.Dispose();
    }

    /// <summary>
    /// 释放连接
    /// </summary>
    private void ReleaseConnection()
    {
        var connection = _connection;
        var ownership = _connectionOwnership;
        _connection = null;
        _connectionOwnership = SqlResourceOwnership.Owned;
        _connectionSource = SqlConnectionSource.Unknown;
        if (ownership == SqlResourceOwnership.Owned)
            connection?.Dispose();
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
    protected virtual T GetParam<T>(string name)
    {
        return (T)ParameterManager?.GetValue(name);
    }

    /// <summary>
    /// 清理
    /// </summary>
    protected void Clear()
    {
        if (_sqlBuilder == null)
            return;
        ClearAfterExecution();
        if (_sqlBuilder is ISqlCommonPartAccessor accessor)
            accessor.ParameterManager?.Clear();
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
