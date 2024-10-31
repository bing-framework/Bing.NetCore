using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders.Params;

/// <summary>
/// 参数字面值解析器
/// </summary>
public class ParamLiteralsResolver : IParamLiteralsResolver
{
    /// <summary>
    /// 获取参数字面值
    /// </summary>
    /// <param name="value">参数值</param>
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
