namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// Sql方言基类
/// </summary>
public abstract class DialectBase : IDialect
{
    /// <summary>
    /// 起始转义标识符
    /// </summary>
    public virtual char OpeningIdentifier { get; } = '[';

    /// <summary>
    /// 结束转义标识符
    /// </summary>
    public virtual char ClosingIdentifier { get; } = ']';

    /// <summary>
    /// 批量操作分隔符
    /// </summary>
    public virtual char BatchSeperator { get; } = ';';

    /// <summary>
    /// 安全名称
    /// </summary>
    /// <param name="name">名称</param>
    /// <returns>按当前方言转义后的安全名称。</returns>
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

    /// <summary>
    /// 获取参数前缀
    /// </summary>
    /// <returns>当前方言使用的参数前缀。</returns>
    public virtual string GetPrefix() => "@";

    /// <summary>
    /// Select子句是否支持As关键字
    /// </summary>
    /// <returns>支持 <c>As</c> 关键字时返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public virtual bool SupportSelectAs() => true;

    /// <summary>
    /// 生成参数名
    /// </summary>
    /// <param name="paramIndex">参数索引</param>
    /// <returns>根据参数索引生成的参数名称。</returns>
    public virtual string GenerateName(int paramIndex) => $"{GetPrefix()}_p_{paramIndex}";

    /// <summary>
    /// 获取参数名
    /// </summary>
    /// <param name="paramName">参数名</param>
    /// <returns>按当前方言格式化后的参数名称。</returns>
    public virtual string GetParamName(string paramName) => paramName;

    /// <summary>
    /// 获取参数值
    /// </summary>
    /// <param name="paramValue">参数值</param>
    /// <returns>按当前方言转换后的参数值。</returns>
    public virtual object GetParamValue(object paramValue) => paramValue;
}