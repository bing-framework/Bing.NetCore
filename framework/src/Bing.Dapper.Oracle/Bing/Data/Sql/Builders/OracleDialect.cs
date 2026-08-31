using Bing.Data.Sql.Builders.Core;
using Bing.Extensions;
using Bing.Helpers;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// Oracle方言
/// </summary>
public sealed class OracleDialect : DialectBase
{
    /// <summary>
    /// 封闭构造方法
    /// </summary>
    private OracleDialect() { }

    /// <summary>
    /// Oracle方言实例
    /// </summary>
    public static OracleDialect Instance { get; } = new();

    /// <inheritdoc />
    public override char OpeningIdentifier => '"';

    /// <inheritdoc />
    public override char ClosingIdentifier => '"';

    /// <inheritdoc />
    /// <returns>Oracle 参数前缀。</returns>
    public override string GetPrefix() => ":";

    /// <inheritdoc />
    /// <returns>Oracle 是否支持 Select 别名语法。</returns>
    public override bool SupportSelectAs() => false;

    /// <inheritdoc />
    /// <returns>按索引生成的 Oracle 参数名。</returns>
    public override string GenerateName(int paramIndex) => $"{GetPrefix()}p_{paramIndex}";

    /// <inheritdoc />
    /// <returns>去除前导冒号后的参数名。</returns>
    public override string GetParamName(string paramName) => paramName.StartsWith(":") ? paramName.TrimStart(':') : paramName;

    /// <inheritdoc />
    /// <returns>转换后的 Oracle 参数值。</returns>
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
