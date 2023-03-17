using System.Collections.Concurrent;
using Bing.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Threading;

/// <summary>
/// 环境数据上下文环境范围提供程序
/// </summary>
/// <typeparam name="T">泛型类型</typeparam>
public class AmbientDataContextAmbientScopeProvider<T> : IAmbientScopeProvider<T>
{
    /// <summary>
    /// 日志
    /// </summary>
    public ILogger<AmbientDataContextAmbientScopeProvider<T>> Logger { get; set; }

    /// <summary>
    /// 范围字典
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static readonly ConcurrentDictionary<string, ScopeItem> ScopeDictionary = new();

    /// <summary>
    /// 环境数据上下文
    /// </summary>
    private readonly IAmbientDataContext _dataContext;

    /// <summary>
    /// 初始化一个<see cref="AmbientDataContextAmbientScopeProvider{T}"/>类型的实例
    /// </summary>
    /// <param name="dataContext">环境数据上下文</param>
    public AmbientDataContextAmbientScopeProvider(IAmbientDataContext dataContext)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        Logger = NullLogger<AmbientDataContextAmbientScopeProvider<T>>.Instance;
    }

    /// <summary>
    /// 获取值
    /// </summary>
    /// <param name="contextKey">上下文键名</param>
    /// <returns>对象值</returns>
    public T GetValue(string contextKey)
    {
        var item = GetCurrentItem(contextKey);
        if (item is null)
            return default;
        return item.Value;
    }

    /// <summary>
    /// 开始范围
    /// </summary>
    /// <param name="contextKey">上下文键名</param>
    /// <param name="value">对象值</param>
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
    /// 获取当前项
    /// </summary>
    /// <param name="contextKey">上下文键名</param>
    private ScopeItem GetCurrentItem(string contextKey)
    {
        return _dataContext.GetData(contextKey) is string objKey ? ScopeDictionary.GetOrDefault(objKey) : null;
    }

    /// <summary>
    /// 范围项
    /// </summary>
    private class ScopeItem
    {
        /// <summary>
        /// 标识
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 外部范围项
        /// </summary>
        public ScopeItem Outer { get; }

        /// <summary>
        /// 值
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// 初始化一个<see cref="ScopeItem"/>类型的实例
        /// </summary>
        /// <param name="value">值</param>
        /// <param name="outer">外部范围项</param>
        public ScopeItem(T value, ScopeItem outer = null)
        {
            Id = Guid.NewGuid().ToString();
            Value = value;
            Outer = outer;
        }
    }
}
