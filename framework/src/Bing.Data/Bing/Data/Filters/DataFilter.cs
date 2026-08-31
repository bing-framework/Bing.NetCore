namespace Bing.Data.Filters;

/// <summary>
/// 基于异步执行流的嵌套数据过滤状态管理器。
/// </summary>
public sealed class DataFilter : IDataFilter
{
    /// <summary>
    /// 当前异步执行流的不可变状态快照。
    /// </summary>
    private readonly AsyncLocal<FilterOverride[]> _overrides = new();

    /// <inheritdoc />
    /// <typeparam name="TFilter">要检查的数据过滤器类型。</typeparam>
    public bool IsEnabled<TFilter>() where TFilter : class
    {
        var filterType = typeof(TFilter);
        var current = _overrides.Value;
        if (current == null)
            return true;
        for (var index = current.Length - 1; index >= 0; index--)
        {
            var item = current[index];
            if (item.FilterType == filterType)
                return item.Enabled;
        }
        return true;
    }

    /// <inheritdoc />
    public IDisposable Enable<TFilter>() where TFilter : class => Push(typeof(TFilter), true);

    /// <inheritdoc />
    public IDisposable Disable<TFilter>() where TFilter : class => Push(typeof(TFilter), false);

    /// <summary>
    /// 压入当前调用专属的状态覆盖。
    /// </summary>
    /// <param name="filterType">过滤器标识类型。</param>
    /// <param name="enabled">覆盖后的启用状态。</param>
    /// <returns>仅移除本次覆盖的作用域句柄。</returns>
    private IDisposable Push(Type filterType, bool enabled)
    {
        var token = Guid.NewGuid();
        var current = _overrides.Value ?? Array.Empty<FilterOverride>();
        var updated = new FilterOverride[current.Length + 1];
        Array.Copy(current, updated, current.Length);
        updated[^1] = new FilterOverride(token, filterType, enabled);
        _overrides.Value = updated;
        return new FilterScope(this, token);
    }

    /// <summary>
    /// 移除指定作用域创建的状态覆盖。
    /// </summary>
    /// <param name="token">作用域唯一标识。</param>
    private void Remove(Guid token)
    {
        var current = _overrides.Value;
        if (current == null || current.Length == 0)
            return;
        var retained = current.Where(item => item.Token != token).ToArray();
        _overrides.Value = retained.Length == 0 ? null : retained;
    }

    /// <summary>
    /// 不可变过滤状态覆盖。
    /// </summary>
    private sealed class FilterOverride
    {
        /// <summary>
        /// 初始化状态覆盖。
        /// </summary>
        /// <param name="token">作用域唯一标识。</param>
        /// <param name="filterType">过滤器标识类型。</param>
        /// <param name="enabled">启用状态。</param>
        public FilterOverride(Guid token, Type filterType, bool enabled)
        {
            Token = token;
            FilterType = filterType;
            Enabled = enabled;
        }

        /// <summary>作用域唯一标识。</summary>
        public Guid Token { get; }

        /// <summary>过滤器标识类型。</summary>
        public Type FilterType { get; }

        /// <summary>启用状态。</summary>
        public bool Enabled { get; }
    }

    /// <summary>
    /// 一次性过滤状态作用域。
    /// </summary>
    private sealed class FilterScope : IDisposable
    {
        /// <summary>
        /// 所属状态管理器。
        /// </summary>
        private DataFilter _owner;

        /// <summary>
        /// 当前作用域唯一标识。
        /// </summary>
        private readonly Guid _token;

        /// <summary>
        /// 初始化作用域。
        /// </summary>
        /// <param name="owner">所属状态管理器。</param>
        /// <param name="token">当前作用域唯一标识。</param>
        public FilterScope(DataFilter owner, Guid token)
        {
            _owner = owner;
            _token = token;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Remove(_token);
        }
    }
}