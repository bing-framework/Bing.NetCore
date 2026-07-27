namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 带参数数量上限的参数管理器。
/// </summary>
internal sealed class ParameterLimitManager : ParameterLimitManagerBase, IParameterManager, IParameterManagerLifecycle
{
    public ParameterLimitManager(IParameterManager inner, int maxParameterCount) : base(inner, maxParameterCount)
    {
    }

    public string GenerateName() => Inner.GenerateName();
    public string NormalizeName(string name) => Inner.NormalizeName(name);

    public void Add(string name, object value, Operator? @operator = null)
    {
        EnsureCanAdd(name);
        Inner.Add(name, value, @operator);
    }

    public IReadOnlyDictionary<string, object> GetParams() => Inner.GetParams();
    public bool Contains(string name) => Inner.Contains(name);
    public object GetValue(string name) => Inner.GetValue(name);
    public IParameterManager Clone() => new ParameterLimitManager(Inner.Clone(), MaxParameterCount);
    public void Clear() => Inner.Clear();
    public IParameterManager CreateEmpty() => new ParameterLimitManager(CreateEmptyInner(), MaxParameterCount);
}