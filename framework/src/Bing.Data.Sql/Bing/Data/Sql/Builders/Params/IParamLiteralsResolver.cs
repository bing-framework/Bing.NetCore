namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 用于诊断 SQL 的参数字面值解析器。
/// </summary>
public interface IParamLiteralsResolver
{
    /// <summary>
    /// 将参数值转换为可嵌入调试 SQL 的字面值。
    /// </summary>
    /// <param name="value">待转换的参数值，可为 null。</param>
    /// <returns>符合当前 Provider SQL 文本规则的参数字面值。</returns>
    string GetParamLiterals(object value);
}
