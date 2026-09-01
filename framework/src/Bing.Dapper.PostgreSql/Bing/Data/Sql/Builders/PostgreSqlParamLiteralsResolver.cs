using Bing.Data.Sql.Builders.Params;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 解析 PostgreSQL 参数字面值。
/// </summary>
public sealed class PostgreSqlParamLiteralsResolver : IParamLiteralsResolver
{
    /// <summary>
    /// 初始化一个 <see cref="PostgreSqlParamLiteralsResolver"/> 类型的实例。
    /// </summary>
    private PostgreSqlParamLiteralsResolver() { }

    /// <summary>
    /// 获取 PostgreSQL 参数字面值解析器单例。
    /// </summary>
    public static PostgreSqlParamLiteralsResolver Instance { get; } = new();

    /// <inheritdoc />
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
