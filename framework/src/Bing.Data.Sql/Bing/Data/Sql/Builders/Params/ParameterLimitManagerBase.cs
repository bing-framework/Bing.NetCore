namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数数量上限管理器基类。
/// </summary>
internal abstract class ParameterLimitManagerBase
{
    /// <summary>
    /// 初始化参数数量上限管理器。
    /// </summary>
    protected ParameterLimitManagerBase(IParameterManager inner, int maxParameterCount)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxParameterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxParameterCount));
        MaxParameterCount = maxParameterCount;
    }

    /// <summary>内部参数管理器。</summary>
    protected IParameterManager Inner { get; }

    /// <summary>最大参数数量。</summary>
    protected int MaxParameterCount { get; }

    /// <summary>验证新增参数不会超过上限。</summary>
    protected void EnsureCanAdd(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Inner.Contains(name) || Inner.GetParams().Count < MaxParameterCount)
            return;
        throw new InvalidOperationException($"SQL 参数数量不能超过 {MaxParameterCount}。");
    }

    /// <summary>创建同配置的空参数管理器。</summary>
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