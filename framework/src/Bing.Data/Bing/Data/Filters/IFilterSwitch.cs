namespace Bing.Data.Filters;

/// <summary>
/// 数据过滤器的作用域级开关。
/// </summary>
public interface IFilterSwitch
{
    /// <summary>
    /// 在当前作用域启用指定过滤器。
    /// </summary>
    /// <typeparam name="TFilterType">过滤器实现类型。</typeparam>
    void EnableFilter<TFilterType>() where TFilterType : class;

    /// <summary>
    /// 在当前作用域临时禁用指定过滤器。
    /// </summary>
    /// <typeparam name="TFilterType">过滤器实现类型。</typeparam>
    /// <returns>用于恢复禁用前状态的释放句柄。</returns>
    IDisposable DisableFilter<TFilterType>() where TFilterType : class;
}
