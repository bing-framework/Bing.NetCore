using System.Runtime.ExceptionServices;
using System.Collections;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Bing.Data.Sql;

/// <summary>为独立读写入口提供命令准备和诊断适配；资源由专用组件组合管理。</summary>
public abstract partial class SqlExecutionBase : IDisposable, IAsyncDisposable, ISqlExecutionRuntimeBindingController
{
    /// <summary>
    /// 当前查询首次解析后固定使用的 SQL Provider。
    /// </summary>
    private ISqlProvider _provider;

    /// <summary>
    /// 参数字面值解析器
    /// </summary>
    private IParamLiteralsResolver _paramLiteralsResolver;

    /// <summary>
    /// Sql 参数绑定器
    /// </summary>
    private ISqlParameterBinder _sqlParameterBinder;

    /// <summary>
    /// 当前执行上下文登记的数据库参数定制器快照。
    /// </summary>
    private IReadOnlyCollection<ISqlDbParameterCustomizer> _dbParameterCustomizers;

    /// <summary>
    /// 外部实体映射解析器
    /// </summary>
    private IEntityMappingResolver _entityMappingResolver;

    /// <summary>
    /// 当前查询实例的上下文标识。
    /// </summary>
    private readonly string _queryContextId = Guid.NewGuid().ToString();



    /// <summary>
    /// 初始化一个 <see cref="SqlExecutionBase"/> 类型的实例。
    /// </summary>
    /// <param name="serviceProvider">用于解析查询依赖的服务提供程序。</param>
    /// <param name="options">SQL 查询配置。</param>
    protected SqlExecutionBase(IServiceProvider serviceProvider, SqlOptions options)
        : this(serviceProvider, options, false) { }

    /// <summary>由程序集内的执行入口选择独立的查询或写入资源。</summary>
    /// <param name="serviceProvider">执行服务。</param>
    /// <param name="options">执行配置。</param>
    /// <param name="queryResources">是否使用查询资源及查询短事务策略。</param>
    private protected SqlExecutionBase(IServiceProvider serviceProvider, SqlOptions options, bool queryResources)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _entityMappingResolver = CaptureEntityMappingResolver(ServiceProvider.GetService<IEntityMappingResolver>());
        Logger = CreateLogger();
        _resources = queryResources
            ? new SqlQueryResourceOwner(ServiceProvider, () => Options, GetCurrentProvider,
                ResolveConnectionString, () => _provider != null || HasBuilderState,
                GetCurrentProviderTransactionCapabilities, EnsureWritableDataSource)
            : new SqlWriteResourceOwner(ServiceProvider, () => Options, GetCurrentProvider,
                ResolveConnectionString, () => _provider != null || HasBuilderState,
                GetCurrentProviderTransactionCapabilities, EnsureWritableDataSource);
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

    /// <summary>
    /// 创建本次执行的输出参数访问器。
    /// </summary>
    /// <param name="parameters">Dapper 实际使用的参数对象。</param>
    /// <returns>支持执行后快照的输出参数访问器；没有输出模型时返回 null。</returns>
    private protected static ISqlOutputParameterAccessor CreateOutputParameterAccessor(object parameters) => parameters switch
    {
        ISqlOutputParameterAccessor accessor => accessor,
        global::Dapper.DynamicParameters dynamicParameters when SqlRuntimeFeatures.ReflectionFallbackEnabled =>
            new DynamicParametersOutputAccessor(dynamicParameters),
        _ => null
    };



    /// <summary>
    /// 获取服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// 获取日志操作
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// 获取或设置Sql配置
    /// </summary>
    protected internal SqlOptions Options { get; set; }

    /// <summary>
    /// 获取实体映射解析器
    /// </summary>
    protected IEntityMappingResolver EntityMappingResolver =>
        _entityMappingResolver ??= CaptureEntityMappingResolver(ServiceProvider.GetService<IEntityMappingResolver>());

    /// <summary>
    /// 获取连接来源
    /// </summary>
    protected SqlConnectionSource ConnectionSource => _resources.ConnectionSource;

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
    /// 获取执行门禁必须具备的 Provider 能力档案。
    /// </summary>
    /// <returns>当前 Provider 声明的能力档案。</returns>
    internal SqlProviderProfile GetRequiredProviderProfile()
    {
        var provider = GetCurrentProvider();
        if (provider is not ISqlProviderProfileProvider { Profile: not null } profileProvider)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMissing, "ProviderProfile",
                provider.Key, $"Provider {provider.Key} 未声明统一能力 Profile。[ProviderProfileMissing]");
        if (profileProvider.Profile.Query == null || profileProvider.Profile.Mutation == null ||
            profileProvider.Profile.Execution == null || profileProvider.Profile.Transaction == null ||
            profileProvider.Profile.Procedure == null || profileProvider.Profile.Limits == null)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMismatch, "ProviderProfile",
                provider.Key, $"Provider {provider.Key} 的统一能力 Profile 不完整。[ProfileMismatch]");
        return profileProvider.Profile;
    }

    /// <summary>
    /// 创建当前 Provider 的结构化能力拒绝异常。
    /// </summary>
    /// <param name="reason">能力拒绝原因。</param>
    /// <param name="capability">被拒绝的能力名称。</param>
    /// <param name="message">保持现有调用方兼容的异常消息。</param>
    /// <returns>附带结构化原因的异常。</returns>
    internal NotSupportedException CreateCapabilityFailure(SqlCapabilityFailureReason reason, string capability,
        string message) => SqlCapabilityFailure.Create(reason, capability, GetCurrentProviderKey(), message);

    /// <summary>
    /// 获取当前 Provider 的事务能力声明；未声明 Profile 时拒绝进入本地事务路径。
    /// </summary>
    /// <returns>当前 Provider 的事务能力。</returns>
    internal SqlProviderTransactionCapabilities GetCurrentProviderTransactionCapabilities()
    {
        var provider = GetCurrentProvider();
        if (provider is not ISqlProviderProfileProvider { Profile: not null } profileProvider)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMissing, "Transaction",
                provider.Key, $"Provider {provider.Key} 未声明事务能力 Profile。[ProviderProfileMissing]");
        if (profileProvider.Profile.Transaction == null)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMismatch, "Transaction",
                provider.Key, $"Provider {provider.Key} 的事务能力 Profile 不完整。[ProfileMismatch]");
        return profileProvider.Profile.Transaction;
    }

    /// <summary>
    /// 获取当前 Provider Key。
    /// </summary>
    /// <returns>当前 Provider Key。</returns>
    internal string GetCurrentProviderKey() => GetCurrentProvider().Key;

    /// <summary>
    /// 获取Sql方言
    /// </summary>
    protected IDialect Dialect => GetCurrentProvider().Dialect;

    /// <summary>
    /// 获取参数字面值解析器
    /// </summary>
    protected IParamLiteralsResolver ParamLiteralsResolver => _paramLiteralsResolver ??= CreateParamLiteralsResolver();

    /// <summary>
    /// 获取Sql 参数绑定器
    /// </summary>
    private protected ISqlParameterBinder SqlParameterBinder => _sqlParameterBinder ??= CreateSqlParameterBinder();

    /// <summary>
    /// 获取生成式写入参数绑定需要的定制器快照。
    /// </summary>
    private protected IReadOnlyCollection<ISqlDbParameterCustomizer> DbParameterCustomizers =>
        _dbParameterCustomizers ??= ServiceProvider.GetServices<ISqlDbParameterCustomizer>()
            .Where(item => item != null).ToArray();

    /// <summary>
    /// 创建 SQL Builder。
    /// </summary>
    /// <returns>由当前 Provider 配置的 SQL Builder。</returns>
    protected virtual ISqlBuilder CreateSqlBuilder() => CreateSqlBuilder(GetCurrentProvider());

    /// <summary>
    /// 创建 SQL Builder。
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

    /// <summary>
    /// 创建当前查询专属的 Builder 共享服务。
    /// </summary>
    /// <remarks>
    /// 服务包可由同一 Builder 的 New 和 Clone 共享，但不得跨 Query 复用，避免泄漏 <see cref="SqlOptions"/>。
    /// </remarks>
    /// <returns>仅可在当前查询及其 Builder 派生实例间共享的服务包。</returns>
    protected virtual SqlBuilderServices CreateSqlBuilderServices()
    {
        var services = new SqlBuilderServices(
            EntityMappingResolver,
            ServiceProvider.GetService<IDatabaseContextAccessor>(),
            ServiceProvider.GetService<ISqlParameterFactory>(),
            ServiceProvider.GetService<SqlMetadataOptions>(),
            Options,
            ServiceProvider.GetService<ISqlDatabaseContextResolver>(),
            ServiceProvider.GetService<ISqlObjectNameFormatter>(),
            ServiceProvider.GetService<ISqlCrossDatabaseQueryValidator>(),
            ServiceProvider.GetService<ISqlTableReferenceValidator>(),
            ServiceProvider.GetService<IEntityModelMetadataProvider>(),
            ServiceProvider.GetServices<ISqlFilter>(),
            ServiceProvider.GetService<IDataFilter>());
        services.DatabaseIdentityResolver = ServiceProvider.GetService<ISqlDatabaseIdentityResolver>() ??
            services.DatabaseIdentityResolver;
        return services;
    }

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
        ServiceProvider.GetService<ISqlParameterBinder>() ??
        throw new InvalidOperationException("未注册 SQL 参数绑定器。请调用 AddSqlCore 注册默认绑定器。");

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


    /// <inheritdoc />
    void ISqlExecutionRuntimeBindingController.BindOwnedConnection(IDbConnection connection, SqlConnectionSource source)
    {
        _resources.EnsureNoUnitOfWorkRuntimeBinding();
        _resources.BindConnection(connection, SqlResourceOwnership.Owned, source);
    }

    /// <inheritdoc />
    void ISqlExecutionRuntimeBindingController.BindExternalConnection(IDbConnection connection, SqlConnectionSource source)
    {
        _resources.EnsureNoUnitOfWorkRuntimeBinding();
        _resources.BindConnection(connection, SqlResourceOwnership.External, source);
    }

    /// <inheritdoc />
    void ISqlExecutionRuntimeBindingController.BindExternalTransactionResolver(Func<IDbTransaction> resolver)
    {
        _resources.EnsureNoUnitOfWorkRuntimeBinding();
        _resources.BindExternalTransactionResolver(resolver);
    }

    /// <inheritdoc />
    void ISqlExecutionRuntimeBindingController.BindDatabaseContext(DatabaseContext context)
    {
        _resources.EnsureNoUnitOfWorkRuntimeBinding();
        _resources.BindDatabaseContext(context);
    }

    /// <inheritdoc />
    void ISqlExecutionRuntimeBindingController.BindEntityMappingResolver(IEntityMappingResolver resolver) =>
        BindEntityMappingResolver(resolver);

    /// <summary>
    /// 一次性绑定外部实体映射解析器。
    /// </summary>
    /// <param name="resolver">实体映射解析器。</param>
    private void BindEntityMappingResolver(IEntityMappingResolver resolver)
    {
        if (resolver == null)
            throw new ArgumentNullException(nameof(resolver));
        _resources.EnsureExecutionAvailable();
        if (HasBuilderState)
            throw new InvalidOperationException("SQL Query 已创建 Builder，不能修改实体映射解析器。");
        _entityMappingResolver = CaptureEntityMappingResolver(resolver);
    }

    /// <summary>
    /// 捕获适用于当前查询生命周期的实体映射解析器。
    /// </summary>
    /// <param name="resolver">待捕获的实体映射解析器。</param>
    /// <returns>快照提供程序返回的稳定解析器，或原解析器。</returns>
    private static IEntityMappingResolver CaptureEntityMappingResolver(IEntityMappingResolver resolver) =>
        resolver is IEntityMappingSnapshotProvider snapshotProvider ? snapshotProvider.CaptureSnapshot() : resolver;

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
    /// 准备命令执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="builder">当前 SQL Builder。</param>
    /// <returns>本次执行的不可变准备快照。</returns>
    private protected SqlPreparedCommand PrepareCommand(ISqlBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);
        // 公开 Parameters 为了保持冻结语义会返回深拷贝；本次执行只取一次，避免同一快照重复复制。
        var parameters = snapshot.Parameters;
        return new SqlPreparedCommand(snapshot.Sql, parameters,
            GetDbParameters(parameters, snapshot.Sql));
    }

    /// <summary>
    /// 准备命令执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="command">已冻结的写入命令。</param>
    /// <returns>本次执行的不可变准备快照。</returns>
    private protected SqlPreparedCommand PrepareCommand(SqlWriteCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        var parameters = command.CreateParameters();
        // 独立 Builder 可以在没有数据源上下文时生成命令。执行时仅补齐缺失的参数类型归属。
        DatabaseType? databaseType = null;
        foreach (var parameter in parameters)
            if (parameter.DatabaseType == null)
                parameter.DatabaseType = databaseType ??= GetDatabaseType();
        // Compatible 模式仍由 Dapper 消费参数对象；生成式参数源只交给静态执行器，避免 Dapper 将其当成空对象。
        var dapperParameters = command.IsGeneratedExecution && ExecutionMode != SqlExecutionMode.Compatible
            ? new SqlGeneratedWriteParameterSource(parameters, command.GeneratedParameterBinder, DbParameterCustomizers)
            : GetDbParameters(parameters, command.Sql);
        return new SqlPreparedCommand(command.Sql, parameters, dapperParameters);
    }

    /// <summary>
    /// 准备命令执行所需的 SQL 和参数。
    /// </summary>
    /// <param name="sql">待执行的 SQL 文本。</param>
    /// <param name="parameters">调用方提供的原始参数源。</param>
    /// <returns>本次执行的不可变准备快照。</returns>
    private protected SqlPreparedCommand PrepareCommand(string sql, object parameters) =>
        new(sql, parameters, GetDbParameters(parameters, sql));

    /// <summary>
    /// 准备文本或过程命令，并配置参数物化后的输出能力校验。
    /// </summary>
    /// <param name="sql">SQL 文本或存储过程名称。</param>
    /// <param name="parameters">调用方提供的参数源。</param>
    /// <returns>本次执行的不可变准备快照。</returns>
    private protected SqlPreparedCommand PrepareCommandWithOutput(string sql, object parameters)
    {
        var command = PrepareCommand(sql, parameters);
        ConfigureOutputParameterSupport(command);
        return command;
    }

    /// <summary>
    /// 为 Dapper 原生参数访问器注入当前 Provider 的输出参数能力。
    /// </summary>
    /// <param name="command">已绑定的准备命令。</param>
    private protected void ConfigureOutputParameterSupport(SqlPreparedCommand command)
    {
        if (command?.DapperParameters is DynamicParametersOutputAccessor accessor)
        {
            var procedure = GetRequiredProviderProfile().Procedure;
            accessor.SetOutputParametersSupported(procedure.SupportsOutputParameters,
                procedure.OutputParametersFailureReason ?? SqlCapabilityFailureReason.ProviderImplementationGap,
                GetCurrentProviderKey());
        }
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
    /// 写入 SQL 跟踪日志。
    /// </summary>
    /// <param name="sql">Sql语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="debugSql">调试Sql语句</param>
    protected virtual void WriteTraceLog(string sql, IReadOnlyDictionary<string, object> parameters, string debugSql)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
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
    /// 写入 SQL 跟踪日志。
    /// </summary>
    /// <param name="sql">当前执行 SQL。</param>
    /// <param name="parameters">原始参数对象。</param>
    private protected void WriteTraceLog(string sql, object parameters)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        WriteTraceLog(sql, ToTraceParameters(parameters), sql);
    }

    /// <summary>
    /// 写入 SQL 跟踪日志。
    /// </summary>
    /// <param name="builder">Sql生成器。</param>
    /// <param name="sql">已生成的Sql语句。</param>
    private protected void WriteTraceLog(ISqlBuilder builder, string sql)
    {
        if (Logger.IsEnabled(LogLevel.Trace) == false)
            return;
        WriteTraceLog(sql, builder.GetParams(), builder.ToDebugSql(sql));
    }

    /// <summary>
    /// 写入 SQL 跟踪日志。
    /// </summary>
    /// <param name="command">当前执行的准备命令。</param>
    private protected void WriteTraceLog(SqlPreparedCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));
        if (command.IsBuilderCommand)
        {
            WriteTraceLog(command.Builder, command.Sql);
            return;
        }
        WriteTraceLog(command.Sql, ToTraceParameters(command.DapperParameters is IDapperParameterSet bound
            ? bound.Parameters : command.ParameterSource), command.DebugSql);
    }

    /// <summary>
    /// 将原生参数源转换为 Trace 使用的名称和值快照。
    /// </summary>
    /// <param name="parameters">原生 SQL 参数源。</param>
    /// <returns>不共享调用方可变状态的参数快照。</returns>
    private protected static IReadOnlyDictionary<string, object> ToTraceParameters(object parameters)
    {
        if (parameters == null)
            return new Dictionary<string, object>();
        if (parameters is IReadOnlyDictionary<string, object> readOnlyDictionary)
            return readOnlyDictionary.ToDictionary(item => item.Key, item => item.Value);
        if (parameters is IDictionary<string, object> dictionary)
            return new Dictionary<string, object>(dictionary);
        if (parameters is IEnumerable<SqlParam> sqlParameters)
            return sqlParameters.Where(parameter => parameter != null)
                .ToDictionary(parameter => parameter.Name, parameter => parameter.Value, StringComparer.OrdinalIgnoreCase);
        if (!SqlRuntimeFeatures.ReflectionFallbackEnabled)
            throw SqlRuntimeFeatures.DynamicExecutionDisabled();
        return parameters.GetType().GetProperties(System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, property => property.GetValue(parameters));
    }


    /// <summary>
    /// 获取当前 Provider 使用的存储过程名称。
    /// </summary>
    /// <param name="procedure">调用方指定的存储过程名称。</param>
    /// <returns>默认原样返回的存储过程名称。</returns>
    protected virtual string GetProcedure(string procedure) => procedure;

    /// <summary>
    /// 获取存储过程命令类型
    /// </summary>
    /// <returns>当前查询使用的存储过程命令类型。</returns>
    protected virtual CommandType GetProcedureCommandType() => CommandType.StoredProcedure;
    /// <summary>获取派生入口是否已创建不可重新绑定的构建状态。</summary>
    private protected virtual bool HasBuilderState => false;

    /// <summary>判断当前命令是否继续执行。</summary>
    protected virtual bool ExecuteBefore() => true;

    /// <summary>通知当前命令执行结束。</summary>
    /// <param name="result">执行结果。</param>
    protected virtual void ExecuteAfter(object result) { }
    /// <summary>
    /// 异步执行查询并完整物化结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行类型。</typeparam>
    /// <param name="connection">数据库连接。</param>
    /// <param name="command">Dapper 命令定义。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>已物化的结果集合。</returns>
    /// <remarks>
    /// 命令已携带取消令牌；统一委托 Dapper 处理标量和对象投影，避免自定义 Reader 解析器在标量类型上产生无效转换。
    /// </remarks>
    protected virtual async Task<List<TResult>> ExecuteMaterializedQueryAsync<TResult>(IDbConnection connection,
        CommandDefinition command, CancellationToken cancellationToken) =>
        (await SqlDynamicCommandRunner.QueryAsync<TResult>(connection, command)).AsList();

    /// <summary>
    /// 创建异步查询使用的 Dapper 命令定义。
    /// </summary>
    /// <param name="sql">待执行的 SQL。</param>
    /// <param name="parameters">数据库参数。</param>
    /// <param name="transaction">当前事务。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="buffered">是否缓冲结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="commandType">命令类型。</param>
    /// <returns>Dapper 命令定义。</returns>
    protected virtual CommandDefinition CreateQueryCommandDefinition(string sql, object parameters,
        IDbTransaction transaction, int? timeout, bool buffered, CancellationToken cancellationToken = default,
        CommandType? commandType = null) => new(sql, parameters, transaction, timeout, commandType,
        buffered ? CommandFlags.Buffered : CommandFlags.None, cancellationToken);

    /// <summary>本次操作是否使用查询所有者创建的主库读短事务。</summary>
    private protected virtual bool IsPrimaryReadTransaction => false;

    private readonly SqlExecutionResources _resources;
    private protected SqlQueryResourceOwner CreateQueryResourceOwner() => (SqlQueryResourceOwner)_resources;
    private protected SqlWriteResourceOwner CreateWriteResourceOwner() => (SqlWriteResourceOwner)_resources;
    protected internal IDbConnection GetExecutionConnection() => _resources.GetExecutionConnection();
    internal SqlTransactionConnectionOwner TransferConnectionOwnership() => _resources.TransferConnectionOwnership();
    internal Func<IDisposable> ExecutionLeaseFactory { get => _resources.ExecutionLeaseFactory; set => _resources.ExecutionLeaseFactory = value; }
    protected IDisposable AcquireExecutionLease() => _resources.AcquireExecutionLease();
    private protected void EnsureExecutionAvailable() => _resources.EnsureExecutionAvailable();
    internal void SetExplicitTransactionScopeMode() => _resources.SetExplicitTransactionScopeMode();
    internal void SetTransactionContext(DatabaseContext context, IDbConnection connection, IDbTransaction transaction, ISqlTransactionScopeLease lease) => _resources.SetTransactionContext(context, connection, transaction, lease);
    protected IDbTransaction GetExecutionTransaction() => _resources.GetExecutionTransaction();
    private protected void MarkPreCancelledUnitOfWorkExecution() => _resources.MarkPreCancelledUnitOfWorkExecution();
    private protected bool HasUnitOfWorkResource => _resources.HasUnitOfWorkResource;
    private protected bool IsUnitOfWorkRootTransactional => _resources.IsUnitOfWorkRootTransactional;
    private protected ISqlOperationCancellationLease CreateOperationCancellationLease(CancellationToken token) => _resources.CreateOperationCancellationLease(token);
    private protected void MarkUnitOfWorkExecutionFailure() => _resources.MarkUnitOfWorkExecutionFailure();
    public void Dispose() => _resources.Dispose();
    ValueTask IAsyncDisposable.DisposeAsync() => ((IAsyncDisposable)_resources).DisposeAsync();
}
