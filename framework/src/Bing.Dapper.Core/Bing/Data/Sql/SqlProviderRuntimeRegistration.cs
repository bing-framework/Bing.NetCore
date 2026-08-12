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
    /// 初始化 Provider 运行时服务实现注册。
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
    /// Provider 唯一标识。
    /// </summary>
    public string ProviderKey { get; }

    /// <summary>
    /// 查询实现类型。
    /// </summary>
    public Type QueryType { get; }

    /// <summary>
    /// 执行器实现类型。
    /// </summary>
    public Type ExecutorType { get; }

    /// <summary>
    /// 多结果集执行器实现类型。
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