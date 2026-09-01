using Bing.DependencyInjection;

namespace Bing.Data.Filters;

/// <summary>
/// 当前作用域的数据过滤器管理器。
/// </summary>
public interface IFilterManager : IFilterSwitch, IScopedDependency
{
    /// <summary>
    /// 按过滤器类型获取已注册的过滤器。
    /// </summary>
    /// <typeparam name="TFilterType">过滤器实现类型。</typeparam>
    /// <returns>指定类型的过滤器实例；未注册时返回 <see langword="null"/>。</returns>
    IFilter GetFilter<TFilterType>() where TFilterType : class;

    /// <summary>
    /// 按运行时类型获取已注册的过滤器。
    /// </summary>
    /// <param name="filterType">过滤器实现类型。</param>
    /// <returns>指定类型的过滤器实例；未注册时返回 <see langword="null"/>。</returns>
    IFilter GetFilter(Type filterType);

    /// <summary>
    /// 判断指定实体类型是否有启用的过滤器。
    /// </summary>
    /// <typeparam name="TEntity">待判断的实体类型。</typeparam>
    /// <returns>至少有一个过滤器对该实体类型启用时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    bool IsEntityEnabled<TEntity>();

    /// <summary>
    /// 判断指定类型的过滤器当前是否启用。
    /// </summary>
    /// <typeparam name="TFilterType">过滤器实现类型。</typeparam>
    /// <returns>过滤器存在且已启用时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    bool IsEnabled<TFilterType>() where TFilterType : class;
}