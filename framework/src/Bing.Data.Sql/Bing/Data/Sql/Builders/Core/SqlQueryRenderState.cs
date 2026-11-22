using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>单次渲染或独立执行副本的工作区，不属于可复用查询结构。</summary>
internal sealed class SqlQueryRenderState
{
    internal SqlQueryRenderState(IParameterManager parameters,
        IReadOnlyDictionary<object, Dictionary<string, string>> names = null, bool filtersApplied = false,
        Dictionary<string, SqlFragmentParameterBinding> parameterKeys = null)
    {
        Parameters = parameters;
        FiltersApplied = filtersApplied;
        SubqueryParameterNames = names == null
            ? new Dictionary<object, Dictionary<string, string>>(ReferenceComparer<object>.Instance)
            : names.ToDictionary(item => item.Key, item => new Dictionary<string, string>(item.Value),
                ReferenceComparer<object>.Instance);
        ParameterKeys = parameterKeys == null ? null : new Dictionary<string, SqlFragmentParameterBinding>(parameterKeys,
            StringComparer.OrdinalIgnoreCase);
    }

    internal IParameterManager Parameters { get; }
    internal Dictionary<object, Dictionary<string, string>> SubqueryParameterNames { get; }
    internal Dictionary<string, SqlFragmentParameterBinding> ParameterKeys { get; set; }
    internal bool FiltersApplied { get; }
    internal string OffsetParam { get; set; }
    internal string LimitParam { get; set; }
    internal IOrderByClause OrderBy { get; set; }
}
