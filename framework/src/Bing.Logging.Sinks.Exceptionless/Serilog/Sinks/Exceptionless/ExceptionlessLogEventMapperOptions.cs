namespace Serilog.Sinks.Exceptionless;

/// <summary>
/// Exceptionless日志事件映射配置
/// </summary>
public sealed class ExceptionlessLogEventMapperOptions
{
    /// <summary>
    /// 最大属性数量
    /// </summary>
    public int MaxPropertyCount { get; set; } = 50;

    /// <summary>
    /// 最大字符串长度
    /// </summary>
    public int MaxStringLength { get; set; } = 2048;

    /// <summary>
    /// 最大集合元素数量
    /// </summary>
    public int MaxCollectionCount { get; set; } = 100;

    /// <summary>
    /// 最大对象嵌套深度
    /// </summary>
    public int MaxDepth { get; set; } = 5;

    /// <summary>
    /// 敏感字段名称片段
    /// </summary>
    public ISet<string> SensitiveNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd", "token", "apikey", "authorization", "cookie", "secret", "creditcard", "ssn", "pin"
    };
}