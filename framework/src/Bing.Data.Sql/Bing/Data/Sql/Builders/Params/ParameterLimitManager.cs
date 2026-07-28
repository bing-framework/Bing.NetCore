namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 带参数数量上限的参数管理器。
/// </summary>
internal sealed class ParameterLimitManager : ParameterLimitManagerBase, IParameterManager, IParameterManagerLifecycle
{
    /// <summary>
    /// 初始化带参数数量上限的参数管理器。
    /// </summary>
    /// <param name="inner">实际保存参数的内部管理器。</param>
    /// <param name="maxParameterCount">允许保存的最大参数数量。</param>
    /// <param name="providerKey">应用参数限制的 Provider 标识。</param>
    public ParameterLimitManager(IParameterManager inner, int maxParameterCount, string providerKey = null)
        : base(inner, maxParameterCount, providerKey)
    {
    }

    /// <inheritdoc />
    public string GenerateName() => Inner.GenerateName();

    /// <inheritdoc />
    public string NormalizeName(string name) => Inner.NormalizeName(name);

    /// <inheritdoc />
    public void Add(string name, object value, Operator? @operator = null)
    {
        EnsureCanAdd(name);
        Inner.Add(name, value, @operator);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetParams() => Inner.GetParams();

    /// <inheritdoc />
    public bool Contains(string name) => Inner.Contains(name);

    /// <inheritdoc />
    public object GetValue(string name) => Inner.GetValue(name);

    /// <inheritdoc />
    /// <remarks>副本保留参数上限，但拥有独立的内部参数集合。</remarks>
    public IParameterManager Clone() => new ParameterLimitManager(CloneInner(), MaxParameterCount, ProviderKey);

    /// <inheritdoc />
    public void Clear() => Inner.Clear();

    /// <inheritdoc />
    /// <remarks>返回实例保留参数上限，但不继承当前参数。</remarks>
    public IParameterManager CreateEmpty() => new ParameterLimitManager(CreateEmptyInner(), MaxParameterCount, ProviderKey);
}