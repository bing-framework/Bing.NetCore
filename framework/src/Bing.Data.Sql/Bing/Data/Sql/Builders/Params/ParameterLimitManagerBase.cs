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
    protected ParameterLimitManagerBase(IParameterManager inner, int maxParameterCount)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxParameterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxParameterCount));
        MaxParameterCount = maxParameterCount;
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
    /// 验证新增参数不会超过配置上限。
    /// </summary>
    /// <param name="name">待添加或替换的参数名称。</param>
    protected void EnsureCanAdd(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Inner.Contains(name) || Inner.GetParams().Count < MaxParameterCount)
            return;
        throw new InvalidOperationException($"SQL 参数数量不能超过 {MaxParameterCount}。");
    }

    /// <summary>
    /// 创建保留内部管理器类型和配置但不包含任何参数的实例。
    /// </summary>
    /// <returns>已清空且不与当前管理器共享参数状态的内部管理器。</returns>
    protected IParameterManager CreateEmptyInner()
    {
        var result = Inner is IParameterManagerLifecycle lifecycle ? lifecycle.CreateEmpty() : Inner.Clone();
        if (result == null)
            throw new InvalidOperationException("参数管理器创建空实例时返回了 null。");
        if (ReferenceEquals(result, Inner))
            throw new InvalidOperationException("参数管理器创建空实例时不能返回当前实例。");
        result.Clear();
        return result;
    }
}