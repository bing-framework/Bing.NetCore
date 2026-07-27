namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 带参数数量上限的增强参数管理器。
/// </summary>
internal sealed class AdvancedParameterLimitManager : ParameterLimitManagerBase, IAdvancedParameterManager,
    IParameterManagerLifecycle
{
    private IAdvancedParameterManager AdvancedInner => (IAdvancedParameterManager)Inner;

    public AdvancedParameterLimitManager(IAdvancedParameterManager inner, int maxParameterCount)
        : base(inner, maxParameterCount)
    {
    }

    public string GenerateName() => Inner.GenerateName();
    public string NormalizeName(string name) => Inner.NormalizeName(name);

    public void Add(string name, object value, Operator? @operator = null)
    {
        EnsureCanAdd(name);
        Inner.Add(name, value, @operator);
    }

    public void Add(SqlParam parameter)
    {
        if (parameter == null)
            return;
        EnsureCanAdd(parameter.Name);
        AdvancedInner.Add(parameter);
    }

    public IReadOnlyDictionary<string, object> GetParams() => Inner.GetParams();
    public IReadOnlyDictionary<string, SqlParam> GetSqlParams() => AdvancedInner.GetSqlParams();
    public IReadOnlyDictionary<string, object> ExportValues() => AdvancedInner.ExportValues();
    public bool Contains(string name) => Inner.Contains(name);
    public object GetValue(string name) => Inner.GetValue(name);
    public IParameterManager Clone() => new AdvancedParameterLimitManager((IAdvancedParameterManager)Inner.Clone(),
        MaxParameterCount);
    public void Clear() => Inner.Clear();
    public IParameterManager CreateEmpty() => new AdvancedParameterLimitManager(
        (IAdvancedParameterManager)CreateEmptyInner(), MaxParameterCount);
}