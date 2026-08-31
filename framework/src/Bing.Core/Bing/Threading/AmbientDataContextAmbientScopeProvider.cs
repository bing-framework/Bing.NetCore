using System.Collections.Concurrent;
using Bing.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Threading;

/// <summary>
/// 基于 <see cref="IAmbientDataContext"/> 保存嵌套环境范围值的提供程序。
/// </summary>
/// <typeparam name="T">环境范围值的类型。</typeparam>
public class AmbientDataContextAmbientScopeProvider<T> : IAmbientScopeProvider<T>
{
    /// <summary>
    /// 获取或设置用于记录范围处理诊断信息的日志记录器。
    /// </summary>
    public ILogger<AmbientDataContextAmbientScopeProvider<T>> Logger { get; set; }

    /// <summary>
    /// 保存所有活动范围项的共享字典。
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, ScopeItem> ScopeDictionary = new();

    /// <summary>
    /// 保存当前执行环境的数据上下文。
    /// </summary>
    private readonly IAmbientDataContext _dataContext;

    /// <summary>
    /// 使用环境数据上下文初始化 <see cref="AmbientDataContextAmbientScopeProvider{T}"/> 的实例。
    /// </summary>
    /// <param name="dataContext">保存当前范围标识的环境数据上下文。</param>
    public AmbientDataContextAmbientScopeProvider(IAmbientDataContext dataContext)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        Logger = NullLogger<AmbientDataContextAmbientScopeProvider<T>>.Instance;
    }

    /// <inheritdoc />
    public T GetValue(string contextKey)
    {
        var item = GetCurrentItem(contextKey);
        if (item is null)
            return default;
        return item.Value;
    }

    /// <inheritdoc />
    /// <remarks>释放范围时删除当前范围项，并恢复创建该范围时捕获的外层范围标识。</remarks>
    public IDisposable BeginScope(string contextKey, T value)
    {
        var item = new ScopeItem(value, GetCurrentItem(contextKey));
        if (!ScopeDictionary.TryAdd(item.Id, item))
            throw new BingFrameworkException($"Can not add item! ScopeDictionary.TryAdd returns false!");
        _dataContext.SetData(contextKey, item.Id);

        return new DisposeAction(() =>
        {
            ScopeDictionary.TryRemove(item.Id, out item);
            if (item.Outer == null)
            {
                _dataContext.SetData(contextKey, null);
                return;
            }

            _dataContext.SetData(contextKey, item.Outer.Id);
        });
    }

    /// <summary>
    /// 根据当前环境上下文中的范围标识获取范围项。
    /// </summary>
    /// <param name="contextKey">用于读取范围标识的上下文键。</param>
    /// <returns>当前范围项；未建立范围或项已移除时返回 <c>null</c>。</returns>
    private ScopeItem GetCurrentItem(string contextKey)
    {
        return _dataContext.GetData(contextKey) is string objKey ? ScopeDictionary.GetOrDefault(objKey) : null;
    }

    /// <summary>
    /// 表示一个范围值及其创建时捕获的外层范围项。
    /// </summary>
    private class ScopeItem
    {
        /// <summary>
        /// 获取用于在共享字典中定位范围项的唯一标识。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 获取创建当前范围时捕获的外层范围项。
        /// </summary>
        public ScopeItem Outer { get; }

        /// <summary>
        /// 获取当前范围保存的值。
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 使用范围值和外层范围项初始化 <see cref="ScopeItem"/> 的实例。
        /// </summary>
        /// <param name="value">当前范围保存的值。</param>
        /// <param name="outer">创建当前范围前的外层范围项。</param>
        public ScopeItem(T value, ScopeItem outer = null)
        {
            Id = Guid.NewGuid().ToString();
            Value = value;
            Outer = outer;
        }
    }
}
