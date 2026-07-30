namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数数量上限管理器基类。
/// </summary>
internal abstract class ParameterLimitManagerBase
{
    /// <summary>
    /// 初始化参数数量上限管理器。
    /// </summary>
    /// <param name="inner">实际保存参数及生成参数名称的内部管理器。</param>
    /// <param name="maxParameterCount">允许同时保存的最大参数数量，必须大于或等于 0。</param>
    /// <param name="providerKey">应用参数限制的 Provider 标识。</param>
    protected ParameterLimitManagerBase(IParameterManager inner, int maxParameterCount, string providerKey = null)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxParameterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxParameterCount));
        MaxParameterCount = maxParameterCount;
        ProviderKey = string.IsNullOrWhiteSpace(providerKey) ? "<未指定>" : providerKey.Trim();
    }

    /// <summary>
    /// 实际保存参数及执行名称规范化的内部管理器。
    /// </summary>
    protected IParameterManager Inner { get; }

    /// <summary>
    /// 当前装饰器允许保存的最大参数数量。
    /// </summary>
    protected int MaxParameterCount { get; }

    /// <summary>
    /// 应用参数数量限制的 Provider 标识。
    /// </summary>
    protected string ProviderKey { get; }

    /// <summary>
    /// 验证新增参数不会超过配置上限。
    /// </summary>
    /// <param name="name">待添加或替换的参数名称。</param>
    protected void EnsureCanAdd(string name)
    {
        name = Inner.NormalizeName(name);
        if (string.IsNullOrWhiteSpace(name) || Inner.Contains(name))
            return;
        var currentCount = Inner.Count;
        if (currentCount < MaxParameterCount)
            return;
        throw new InvalidOperationException(
            $"SQL Provider '{ProviderKey}' 的参数数量超出上限。当前参数数量: {currentCount}；尝试添加后数量: {currentCount + 1}；最大参数数量: {MaxParameterCount}。");
    }

    /// <summary>
    /// 克隆内部参数管理器并确保副本不与当前实例共享参数状态。
    /// </summary>
    /// <returns>独立的内部参数管理器副本。</returns>
    protected IParameterManager CloneInner()
    {
        var result = Inner.Clone();
        if (result == null)
            throw new InvalidOperationException("参数管理器克隆时返回了 null。");
        if (ReferenceEquals(result, Inner))
            throw new InvalidOperationException("参数管理器克隆时不能返回当前实例。");
        return result;
    }

    /// <summary>
    /// 创建保留内部管理器类型和配置但不包含任何参数的实例。
    /// </summary>
    /// <returns>已清空且不与当前管理器共享参数状态的内部管理器。</returns>
    protected IParameterManager CreateEmptyInner()
    {
        var result = Inner.CreateEmpty();
        if (result == null)
            throw new InvalidOperationException("参数管理器创建空实例时返回了 null。");
        if (ReferenceEquals(result, Inner))
            throw new InvalidOperationException("参数管理器创建空实例时不能返回当前实例。");
        result.Clear();
        return result;
    }
}