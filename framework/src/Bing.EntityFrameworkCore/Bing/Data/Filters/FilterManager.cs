using System.Collections.Concurrent;

namespace Bing.Data.Filters;

/// <summary>
/// 数据过滤器管理器
/// </summary>
public class FilterManager : IFilterManager
{
    /// <summary>
    /// 过滤器字典
    /// </summary>
    private readonly ConcurrentDictionary<Type, IFilter> _filters;

    /// <summary>
    /// 服务提供程序
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 当前异步执行流的共享过滤状态。
    /// </summary>
    private readonly IDataFilter _dataFilter;

    /// <summary>
    /// 初始化一个<see cref="FilterManager"/>类型的实例
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <param name="dataFilter">当前执行流的共享过滤状态。</param>
    public FilterManager(IServiceProvider serviceProvider, IDataFilter dataFilter = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _dataFilter = dataFilter ?? serviceProvider.GetService(typeof(IDataFilter)) as IDataFilter ?? new DataFilter();
        _filters = new ConcurrentDictionary<Type, IFilter>();
    }

    /// <summary>
    /// 启用过滤器
    /// </summary>
    /// <typeparam name="TFilterType">过滤器类型</typeparam>
    public IDisposable EnableFilter<TFilterType>() where TFilterType : class
    {
        return _dataFilter.Enable<TFilterType>();
    }

    /// <summary>
    /// 禁用过滤器
    /// </summary>
    /// <typeparam name="TFilterType">过滤器类型</typeparam>
    public IDisposable DisableFilter<TFilterType>() where TFilterType : class
    {
        return _dataFilter.Disable<TFilterType>();
    }

    /// <summary>
    /// 获取过滤器
    /// </summary>
    /// <typeparam name="TFilterType">过滤器类型</typeparam>
    public IFilter GetFilter<TFilterType>() where TFilterType : class
    {
        return GetFilter(typeof(TFilterType));
    }

    /// <summary>
    /// 获取过滤器
    /// </summary>
    /// <param name="filterType">过滤器类型</param>
    public IFilter GetFilter(Type filterType)
    {
        if (filterType == null)
            return null;
        if (_filters.TryGetValue(filterType, out var cached))
            return cached;
        var serviceType = typeof(IFilter<>).MakeGenericType(filterType);
        var filter = _serviceProvider.GetService(serviceType) as IFilter;
        if (filter != null)
            _filters.TryAdd(filterType, filter);
        return filter;
    }

    /// <summary>
    /// 实体是否启用过滤器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public bool IsEntityEnabled<TEntity>()
    {
        var filter = GetFilter<ISoftDelete>();
        return filter != null && _dataFilter.IsEnabled<ISoftDelete>() && filter.IsEntityEnabled<TEntity>();
    }

    /// <summary>
    /// 过滤器是否启用
    /// </summary>
    /// <typeparam name="TFilterType">过滤器类型</typeparam>
    public bool IsEnabled<TFilterType>() where TFilterType : class
    {
        return GetFilter<TFilterType>() != null && _dataFilter.IsEnabled<TFilterType>();
    }
}
