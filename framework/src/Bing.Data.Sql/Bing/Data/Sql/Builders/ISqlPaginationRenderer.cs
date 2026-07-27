namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 分页渲染器。
/// </summary>
public interface ISqlPaginationRenderer
{
    /// <summary>
    /// 渲染分页 SQL 片段。
    /// </summary>
    /// <param name="offsetParameterName">表示跳过行数的已格式化参数名。</param>
    /// <param name="limitParameterName">表示返回行数上限的已格式化参数名。</param>
    /// <returns>不包含前后空白的 Provider 分页 SQL 片段。</returns>
    string Render(string offsetParameterName, string limitParameterName);
}