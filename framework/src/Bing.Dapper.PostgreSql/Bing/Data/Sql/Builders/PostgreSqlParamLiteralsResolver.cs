using Bing.Data.Sql.Builders.Params;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// PostgreSql参数字面值解析器
/// </summary>
public sealed class PostgreSqlParamLiteralsResolver : IParamLiteralsResolver
{
    /// <summary>
    /// 封闭构造函数
    /// </summary>
    private PostgreSqlParamLiteralsResolver() { }

    /// <summary>
    /// PostgreSql参数字面值解析器实例
    /// </summary>
    public static PostgreSqlParamLiteralsResolver Instance { get; } = new();

    /// <inheritdoc />
    /// <returns>适用于 PostgreSQL SQL 文本的参数字面值。</returns>
    public string GetParamLiterals(object value)
    {
        if (value == null)
            return "''";
        switch (value.GetType().Name.ToLower())
        {
            case "boolean":
                return Conv.ToBool(value) ? "true" : "false";

            case "int16":
            case "int32":
            case "int64":
            case "single":
            case "double":
            case "decimal":
                return value.SafeString();

            default:
                return $"'{value}'";
        }
    }
}
