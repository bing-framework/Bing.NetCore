namespace Bing.Data.Queries;

/// <summary>
/// 提供关键字和分页条件的查询参数。
/// </summary>
public class QueryParameter : Pager, IQueryParameter
{
    /// <inheritdoc />
    public string Keyword { get; set; }
}
