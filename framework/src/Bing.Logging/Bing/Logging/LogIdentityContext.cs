namespace Bing.Logging;

/// <summary>
/// 日志身份上下文
/// </summary>
public sealed class LogIdentityContext
{
    /// <summary>
    /// 初始化一个<see cref="LogIdentityContext"/>类型的实例
    /// </summary>
    public LogIdentityContext(string userId = null, string tenantId = null, string sessionId = null)
    {
        UserId = userId;
        TenantId = tenantId;
        SessionId = sessionId;
    }

    /// <summary>
    /// 用户标识
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// 租户标识
    /// </summary>
    public string TenantId { get; }

    /// <summary>
    /// 会话标识
    /// </summary>
    public string SessionId { get; }
}