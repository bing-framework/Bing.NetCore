namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数数量上限管理器基类。
/// </summary>
internal abstract class ParameterLimitManagerBase
{
    /// <summary>
    /// 初始化参数数量上限管理器。
    /// </summary>
    /// <param name="inner">内部参数管理器。</param>
    /// <param name="maxParameterCount">最大参数数量。</param>
    protected ParameterLimitManagerBase(IParameterManager inner, int maxParameterCount)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (maxParameterCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxParameterCount));
        MaxParameterCount = maxParameterCount;
    }

    /// <summary>
    /// 内部参数管理器。
    /// </summary>
    protected IParameterManager Inner { get; }

    /// <summary>
    /// 最大参数数量。
    /// </summary>
    protected int MaxParameterCount { get; }

    /// <summary>
    /// 验证新增参数不会超过上限。
    /// </summary>
    /// <param name="name">参数名。</param>
    protected void EnsureCanAdd(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Inner.Contains(name) || Inner.GetParams().Count < MaxParameterCount)
            return;
        throw new InvalidOperationException($"SQL 参数数量不能超过 {MaxParameterCount}。");
    }

    /// <summary>
    /// 创建同配置的空参数管理器。
    /// </summary>
    /// <returns>空参数管理器。</returns>
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

/// <summary>
/// 带参数数量上限的参数管理器。
/// </summary>
internal sealed class ParameterLimitManager : ParameterLimitManagerBase, IParameterManager, IParameterManagerLifecycle
{
    /// <summary>
    /// 初始化一个 <see cref="ParameterLimitManager"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部参数管理器。</param>
    /// <param name="maxParameterCount">最大参数数量。</param>
    public ParameterLimitManager(IParameterManager inner, int maxParameterCount) : base(inner, maxParameterCount)
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
    public IParameterManager Clone() => new ParameterLimitManager(Inner.Clone(), MaxParameterCount);

    /// <inheritdoc />
    public void Clear() => Inner.Clear();

    /// <inheritdoc />
    public IParameterManager CreateEmpty() => new ParameterLimitManager(CreateEmptyInner(), MaxParameterCount);
}

/// <summary>
/// 带参数数量上限的增强参数管理器。
/// </summary>
internal sealed class AdvancedParameterLimitManager : ParameterLimitManagerBase, IAdvancedParameterManager,
    IParameterManagerLifecycle
{
    /// <summary>
    /// 内部增强参数管理器。
    /// </summary>
    private IAdvancedParameterManager AdvancedInner => (IAdvancedParameterManager)Inner;

    /// <summary>
    /// 初始化一个 <see cref="AdvancedParameterLimitManager"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部增强参数管理器。</param>
    /// <param name="maxParameterCount">最大参数数量。</param>
    public AdvancedParameterLimitManager(IAdvancedParameterManager inner, int maxParameterCount)
        : base(inner, maxParameterCount)
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
    public void Add(SqlParam parameter)
    {
        if (parameter == null)
            return;
        EnsureCanAdd(parameter.Name);
        AdvancedInner.Add(parameter);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetParams() => Inner.GetParams();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => AdvancedInner.GetSqlParams();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> ExportValues() => AdvancedInner.ExportValues();

    /// <inheritdoc />
    public bool Contains(string name) => Inner.Contains(name);

    /// <inheritdoc />
    public object GetValue(string name) => Inner.GetValue(name);

    /// <inheritdoc />
    public IParameterManager Clone() => new AdvancedParameterLimitManager((IAdvancedParameterManager)Inner.Clone(),
        MaxParameterCount);

    /// <inheritdoc />
    public void Clear() => Inner.Clear();

    /// <inheritdoc />
    public IParameterManager CreateEmpty() => new AdvancedParameterLimitManager(
        (IAdvancedParameterManager)CreateEmptyInner(), MaxParameterCount);
}