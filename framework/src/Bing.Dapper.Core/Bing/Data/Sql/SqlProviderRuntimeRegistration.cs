namespace Bing.Data.Sql;

/// <summary>
/// Provider 运行时服务注册。
/// </summary>
/// <remarks>
/// 该类型是 Dapper Provider 接入查询和执行工厂的最小公开 SPI。
/// 它在构造后不可变，避免 Provider 注册顺序改变已注册的运行时路由。
/// </remarks>
public sealed class SqlProviderRuntime
{
    /// <summary>
    /// 初始化一个 <see cref="SqlProviderRuntime"/> 类型的实例。
    /// </summary>
    /// <param name="providerKey">Provider 唯一标识。</param>
    /// <param name="queryType">实现 <see cref="ISqlQuery"/> 的查询类型。</param>
    /// <param name="executorType">实现 <see cref="ISqlExecutor"/> 的执行器类型。</param>
    /// <param name="multipleQueryExecutorType">可选的多结果集执行器类型。</param>
    public SqlProviderRuntime(string providerKey, Type queryType, Type executorType,
        Type multipleQueryExecutorType = null)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            throw new ArgumentException("SQL Provider Key 不能为空。", nameof(providerKey));
        ProviderKey = providerKey.Trim();
        QueryType = ValidateImplementationType(queryType, typeof(ISqlQuery), nameof(queryType));
        ExecutorType = ValidateImplementationType(executorType, typeof(ISqlExecutor), nameof(executorType));
        MultipleQueryExecutorType = multipleQueryExecutorType == null
            ? null
            : ValidateImplementationType(multipleQueryExecutorType, typeof(ISqlMultipleQueryExecutor),
                nameof(multipleQueryExecutorType));
    }

    /// <summary>
    /// 创建使用显式查询与配置工厂的 Provider 运行时。
    /// </summary>
    /// <param name="providerKey">Provider 注册键。</param>
    /// <param name="queryType">查询实现类型。</param>
    /// <param name="executorType">执行器实现类型。</param>
    /// <param name="multipleQueryExecutorType">多结果集查询执行器类型。</param>
    /// <param name="queryFactory">使用服务提供器和选项创建查询的工厂。</param>
    /// <param name="optionsFactory">创建 SQL 选项的工厂。</param>
    /// <param name="templateFactory">从依赖注入容器取得选项模板的工厂。</param>
    /// <returns>使用指定类型及工厂委托的 Provider 运行时注册。</returns>
    /// <remarks>显式工厂避免动态构造查询和选项，供 Native AOT 注册使用。</remarks>
    public static SqlProviderRuntime CreateWithQueryFactory(string providerKey, Type queryType, Type executorType, Type multipleQueryExecutorType,
        Func<IServiceProvider, SqlOptions, ISqlQuery> queryFactory, Func<SqlOptions> optionsFactory,
        Func<IServiceProvider, SqlOptions> templateFactory) =>
        new(providerKey, queryType, executorType, multipleQueryExecutorType, queryFactory, optionsFactory, templateFactory);

    /// <summary>使用静态委托创建查询、写入及多结果执行器，避免动态激活和配置反射复制。</summary>
    /// <param name="providerKey">Provider 键。</param>
    /// <param name="queryType">查询实现类型。</param>
    /// <param name="executorType">写入实现类型。</param>
    /// <param name="multipleQueryExecutorType">多结果实现类型。</param>
    /// <param name="queryFactory">查询工厂。</param>
    /// <param name="executorFactory">写入工厂。</param>
    /// <param name="multipleFactory">多结果工厂；未提供此能力时为空。</param>
    /// <param name="optionsFactory">配置工厂。</param>
    /// <param name="templateFactory">配置模板获取委托。</param>
    /// <returns>当前 Provider 的静态运行时注册。</returns>
    public static SqlProviderRuntime CreateWithFactories(string providerKey, Type queryType, Type executorType,
        Type multipleQueryExecutorType, Func<IServiceProvider, SqlOptions, ISqlQuery> queryFactory,
        Func<IServiceProvider, SqlOptions, ISqlExecutor> executorFactory,
        Func<IServiceProvider, SqlOptions, ISqlMultipleQueryExecutor> multipleFactory,
        Func<Type, SqlOptions> optionsFactory, Func<IServiceProvider, Type, SqlOptions> templateFactory)
    {
        var runtime = new SqlProviderRuntime(providerKey, queryType, executorType, multipleQueryExecutorType,
            queryFactory, () => optionsFactory(typeof(ISqlQuery)),
            provider => templateFactory(provider, typeof(ISqlQuery)),
            executorFactory ?? throw new ArgumentNullException(nameof(executorFactory)), multipleFactory);
        runtime.ServiceOptionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        runtime.ServiceTemplateFactory = templateFactory ?? throw new ArgumentNullException(nameof(templateFactory));
        return runtime;
    }

    /// <summary>
    /// 初始化一个 <see cref="SqlProviderRuntime" /> 类型的实例。
    /// </summary>
    /// <param name="providerKey">数据库提供程序的规范化键。</param>
    /// <param name="queryType">用于执行查询的查询实现类型。</param>
    /// <param name="executorType">用于执行 SQL 操作的执行器类型。</param>
    /// <param name="multipleQueryExecutorType">用于执行多结果集 SQL 查询的执行器类型。</param>
    /// <param name="queryFactory">用于创建 SQL 查询对象的工厂委托。</param>
    /// <param name="optionsFactory">用于创建 SQL 选项的工厂。</param>
    /// <param name="templateFactory">用于根据服务提供程序创建 SQL 选项模板的工厂。</param>
    /// <param name="executorFactory">创建写入执行器的静态工厂。</param>
    /// <param name="multipleFactory">创建多结果执行器的静态工厂。</param>
    private SqlProviderRuntime(string providerKey, Type queryType, Type executorType, Type multipleQueryExecutorType,
        Func<IServiceProvider, SqlOptions, ISqlQuery> queryFactory, Func<SqlOptions> optionsFactory,
        Func<IServiceProvider, SqlOptions> templateFactory,
        Func<IServiceProvider, SqlOptions, ISqlExecutor> executorFactory = null,
        Func<IServiceProvider, SqlOptions, ISqlMultipleQueryExecutor> multipleFactory = null)
        : this(providerKey, queryType, executorType, multipleQueryExecutorType)
    {
        QueryFactory = queryFactory ?? throw new ArgumentNullException(nameof(queryFactory));
        OptionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        TemplateFactory = templateFactory ?? throw new ArgumentNullException(nameof(templateFactory));
        ExecutorFactory = executorFactory;
        MultipleFactory = multipleFactory;
    }

    /// <summary>
    /// 获取创建 SQL 查询实例的委托。
    /// </summary>
    internal Func<IServiceProvider, SqlOptions, ISqlQuery> QueryFactory { get; }
    internal Func<IServiceProvider, SqlOptions, ISqlExecutor> ExecutorFactory { get; }
    internal Func<IServiceProvider, SqlOptions, ISqlMultipleQueryExecutor> MultipleFactory { get; }
    /// <summary>
    /// 获取创建当前 Provider 配置的委托。
    /// </summary>
    internal Func<SqlOptions> OptionsFactory { get; }
    /// <summary>
    /// 获取按服务提供器创建配置模板的委托。
    /// </summary>
    internal Func<IServiceProvider, SqlOptions> TemplateFactory { get; }
    internal Func<Type, SqlOptions> ServiceOptionsFactory { get; private set; }
    internal Func<IServiceProvider, Type, SqlOptions> ServiceTemplateFactory { get; private set; }

    /// <summary>
    /// 获取Provider 唯一标识。
    /// </summary>
    public string ProviderKey { get; }

    /// <summary>
    /// 获取查询实现类型。
    /// </summary>
    public Type QueryType { get; }

    /// <summary>
    /// 获取执行器实现类型。
    /// </summary>
    public Type ExecutorType { get; }

    /// <summary>
    /// 获取多结果集执行器实现类型。
    /// </summary>
    public Type MultipleQueryExecutorType { get; }

    /// <summary>
    /// 根据服务契约解析运行时实现类型。
    /// </summary>
    /// <param name="serviceType">受支持的 SQL 服务契约类型。</param>
    /// <returns>对应实现类型；未注册可选多结果集执行器时返回 null。</returns>
    internal Type Resolve(Type serviceType)
    {
        if (serviceType == typeof(ISqlQuery))
            return QueryType;
        if (serviceType == typeof(ISqlExecutor))
            return ExecutorType;
        if (serviceType == typeof(ISqlMultipleQueryExecutor))
            return MultipleQueryExecutorType;
        return null;
    }

    /// <summary>
    /// 验证 Provider 实现类型。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <param name="serviceType">服务契约类型。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>已验证的实现类型。</returns>
    private static Type ValidateImplementationType(Type implementationType, Type serviceType, string parameterName)
    {
        if (implementationType == null)
            throw new ArgumentNullException(parameterName);
        if (implementationType.IsAbstract || serviceType.IsAssignableFrom(implementationType) == false)
            throw new ArgumentException($"类型 {implementationType.FullName} 未实现 {serviceType.FullName}。", parameterName);
        return implementationType;
    }
}
