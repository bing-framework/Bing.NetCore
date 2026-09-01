using Bing.Data.Sql.Builders.Core;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// 提供 Oracle SQL 方言规则。
/// </summary>
public sealed class OracleDialect : DialectBase
{
    /// <summary>
    /// 初始化一个 <see cref="OracleDialect"/> 类型的实例。
    /// </summary>
    private OracleDialect() { }

    /// <summary>
    /// 获取 Oracle 方言单例。
    /// </summary>
    public static OracleDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '"';

    /// <inheritdoc />
    public override char ClosingIdentifier => '"';

    /// <inheritdoc />
    public override string GetPrefix() => ":";

    /// <inheritdoc />
    public override bool SupportSelectAs() => false;

    /// <inheritdoc />
    public override string GenerateName(int paramIndex) => $"{GetPrefix()}p_{paramIndex}";

    /// <inheritdoc />
    public override string GetParamName(string paramName) => paramName.StartsWith(":") ? paramName.TrimStart(':') : paramName;

    /// <inheritdoc />
    public override object GetParamValue(object paramValue)
    {
        if (paramValue == null)
            return "";
        switch (paramValue.GetType().Name.ToLower())
        {
            case "boolean":
                return Conv.ToBool(paramValue) ? 1 : 0;
            case "int16":
            case "int32":
            case "int64":
            case "single":
            case "double":
            case "decimal":
                return paramValue.SafeString();
            default:
                return $"{paramValue}";
        }
    }
}
