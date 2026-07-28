namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定解析器
/// </summary>
public interface ISqlParameterResolver
{
    /// <summary>
    /// 解析参数绑定结果
    /// </summary>
    /// <param name="context">参数绑定上下文</param>
    /// <returns>参数绑定结果</returns>
    SqlParameterBindingResult Resolve(SqlParameterBindingContext context);
}