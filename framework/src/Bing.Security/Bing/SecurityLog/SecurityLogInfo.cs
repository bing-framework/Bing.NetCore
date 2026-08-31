namespace Bing.SecurityLog;

/// <summary>
/// 表示一次安全相关操作的主体、动作、客户端和租户审计信息。
/// </summary>
[Serializable]
public class SecurityLogInfo
{
    /// <summary>
    /// 获取或设置产生安全日志的应用程序名称。
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// 获取或设置安全操作关联的身份或资源标识。
    /// </summary>
    public string Identity { get; set; }

    /// <summary>
    /// 获取或设置执行的安全操作名称。
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// 获取或设置安全日志的附加结构化属性。
    /// </summary>
    public Dictionary<string,object> ExtraProperties { get; set; }

    /// <summary>
    /// 获取或设置发起安全操作的用户标识。
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// 获取或设置发起安全操作的用户名。
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 获取或设置安全操作所属的租户标识。
    /// </summary>
    public string TenantId { get; set; }

    /// <summary>
    /// 获取或设置安全操作所属的租户名称。
    /// </summary>
    public string TenantName { get; set; }

    /// <summary>
    /// 获取或设置发起安全操作的客户端标识。
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// 获取或设置用于关联同一请求链路日志的关联标识。
    /// </summary>
    public string CorrelationId { get; set; }

    /// <summary>
    /// 获取或设置客户端 IP 地址。
    /// </summary>
    public string ClientIpAddress { get; set; }

    /// <summary>
    /// 获取或设置客户端浏览器或用户代理信息。
    /// </summary>
    public string BrowserInfo { get; set; }

    /// <summary>
    /// 获取或设置安全日志的创建时间。
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// 初始化 <see cref="SecurityLogInfo"/> 的实例及空附加属性字典。
    /// </summary>
    public SecurityLogInfo()
    {
        ExtraProperties = new Dictionary<string, object>();
    }

    /// <summary>
    /// 返回用于诊断输出的安全日志摘要。
    /// </summary>
    /// <returns>包含应用程序、身份和操作信息的安全日志摘要。</returns>
    public override string ToString() => $"SECURITY LOG: [{ApplicationName} - {Identity} -{Action}]";
}
