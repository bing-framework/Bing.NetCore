namespace Bing.Auditing.Persistence;

/// <summary>
/// 配置审计日志的持久化范围与大小限制。
/// </summary>
public sealed class AuditLogPersistenceOptions
{
    /// <summary>
    /// 获取允许持久化值的实体属性名称。
    /// </summary>
    /// <remarks>默认空白名单；参数、异常正文及扩展属性不会保存。名称按 Ordinal 比较。</remarks>
    public ISet<string> AllowedPropertyNames { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// 获取或设置单个文本明细的最大字符数。
    /// </summary>
    /// <remarks>默认值为 512。</remarks>
    public int MaxFieldLength { get; set; } = 512;

    /// <summary>
    /// 获取或设置明细 JSON 的最大 UTF-8 字节数。
    /// </summary>
    /// <remarks>默认值为 65536。</remarks>
    public int MaxPayloadBytes { get; set; } = 65536;

    /// <summary>
    /// 获取或设置一次日志允许的明细总数。
    /// </summary>
    /// <remarks>操作、实体、属性变更及异常类型共同计数；超限时拒绝保存。</remarks>
    /// <remarks>默认值为 100。</remarks>
    public int MaxDetailCount { get; set; } = 100;
}
