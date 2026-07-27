namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 分页渲染器。
/// </summary>
public interface ISqlPaginationRenderer
{
    /// <summary>
    /// 渲染分页 SQL 片段。
    /// </summary>
    string Render(string offsetParameterName, string limitParameterName);
}