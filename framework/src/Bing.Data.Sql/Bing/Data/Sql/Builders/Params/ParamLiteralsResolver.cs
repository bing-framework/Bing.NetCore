using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数字面值解析器
/// </summary>
public sealed class ParamLiteralsResolver : IParamLiteralsResolver
{
    /// <summary>
    /// 默认参数字面值解析器实例。
    /// </summary>
    public static ParamLiteralsResolver Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="ParamLiteralsResolver"/> 类型的实例。
    /// </summary>
    public ParamLiteralsResolver() { }

    /// <summary>
    /// 获取参数字面值
    /// </summary>
    /// <param name="value">参数值</param>
    /// <returns>可嵌入 SQL 的参数字面值文本。</returns>
    public string GetParamLiterals(object value)
    {
        if (value == null)
            return "''";
        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Boolean:
                return Conv.ToBool(value) ? "1" : "0";
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return value.SafeString();
            case TypeCode.DateTime:
                return $"'{value:yyyy-MM-dd HH:mm:ss}'";
            default:
                return $"'{value}'";
        }
    }
}
