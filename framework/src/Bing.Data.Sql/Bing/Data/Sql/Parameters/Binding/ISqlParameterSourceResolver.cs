namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数源解析器
/// </summary>
public interface ISqlParameterSourceResolver
{
    /// <summary>
    /// 判断是否支持参数源
    /// </summary>
    /// <param name="source">参数源</param>
    /// <returns>支持时返回 true</returns>
    bool CanResolve(object source);

    /// <summary>
    /// 尝试解析参数值
    /// </summary>
    /// <param name="source">参数源</param>
    /// <param name="parameterName">标准参数名称</param>
    /// <param name="value">参数值</param>
    /// <returns>找到参数时返回 true</returns>
    bool TryResolve(object source, string parameterName, out object value);
}