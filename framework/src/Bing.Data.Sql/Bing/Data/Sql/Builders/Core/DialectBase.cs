namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql方言基类
/// </summary>
public abstract class DialectBase : IDialect
{
    /// <inheritdoc />
    public virtual char OpeningIdentifier { get; } = '[';

    /// <inheritdoc />
    public virtual char ClosingIdentifier { get; } = ']';

    /// <inheritdoc />
    public virtual char BatchSeperator { get; } = ';';

    /// <inheritdoc />
    public virtual string SafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        if (name == "*")
            return name;
        if (name.IndexOfAny(new[] { '\r', '\n', ';' }) >= 0)
            throw new ArgumentException("标识符包含无效字符。", nameof(name));
        return GetSafeName(FilterName(name));
    }

    /// <summary>
    /// 过滤名称
    /// </summary>
    /// <param name="name">待过滤的名称。</param>
    /// <returns>移除名称首尾常见标识符包裹符后的文本。</returns>
    protected string FilterName(string name) => name.Trim().TrimStart('[').TrimEnd(']').TrimStart('`').TrimEnd('`').TrimStart('"').TrimEnd('"');

    /// <summary>
    /// 获取安全名称
    /// </summary>
    /// <param name="name">名称</param>
    /// <returns>按当前方言转义后的名称。</returns>
    protected virtual string GetSafeName(string name) =>
        $"{OpeningIdentifier}{name.Replace(ClosingIdentifier.ToString(), new string(ClosingIdentifier, 2))}{ClosingIdentifier}";

    /// <inheritdoc />
    public virtual string GetPrefix() => "@";

    /// <inheritdoc />
    /// <remarks>
    /// Select子句是否支持As关键字
    /// </remarks>
    public virtual bool SupportSelectAs() => true;

    /// <inheritdoc />
    public virtual string GenerateName(int paramIndex) => $"{GetPrefix()}_p_{paramIndex}";

    /// <inheritdoc />
    public virtual string GetParamName(string paramName) => paramName;

    /// <inheritdoc />
    public virtual object GetParamValue(object paramValue) => paramValue;
}