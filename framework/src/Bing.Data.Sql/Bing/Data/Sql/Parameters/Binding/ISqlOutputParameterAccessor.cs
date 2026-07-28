namespace Bing.Data.Sql;

/// <summary>
/// SQL 输出参数访问器
/// </summary>
public interface ISqlOutputParameterAccessor
{
    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <param name="name">参数名称</param>
    /// <returns>输出参数值</returns>
    object GetValue(string name);

    /// <summary>
    /// 获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <returns>转换后的输出参数值</returns>
    T GetValue<T>(string name);

    /// <summary>
    /// 尝试获取输出参数值
    /// </summary>
    /// <typeparam name="T">目标类型</typeparam>
    /// <param name="name">参数名称</param>
    /// <param name="value">输出参数值</param>
    /// <returns>获取成功时返回 true</returns>
    bool TryGetValue<T>(string name, out T value);
}