using Bing.Data.Sql.Builders.Core;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Oracle方言
/// </summary>
public class OracleDialect : DialectBase
{
    /// <summary>
    /// 封闭构造方法
    /// </summary>
    private OracleDialect() { }

    /// <summary>
    /// Oracle方言实例
    /// </summary>
    public static IDialect Instance => new OracleDialect();

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
