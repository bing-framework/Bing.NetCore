namespace Bing.Emailing;

/// <summary>
/// 配置 SMTP 邮件发送和队列轮询行为。
/// </summary>
public class EmailConfig
{
    /// <summary>
    /// 获取或设置 SMTP 服务器主机名或地址。
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 获取或设置 SMTP 服务端口，默认值为 <c>25</c>。
    /// </summary>
    public int Port { get; set; } = 25;

    /// <summary>
    /// 获取或设置 SMTP 认证用户名；认证不启用时可以不设置。
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 获取或设置 SMTP 认证密码。
    /// </summary>
    /// <remarks>该值属于敏感凭据，不应写入日志、异常详情或诊断输出。</remarks>
    public string Password { get; set; }

    /// <summary>
    /// 获取或设置 SMTP 认证使用的可选域名。
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// 获取或设置是否为 SMTP 连接启用 SSL/TLS。
    /// </summary>
    public bool EnableSsl { get; set; }

    /// <summary>
    /// 获取或设置是否使用当前环境提供的默认凭据进行 SMTP 认证。
    /// </summary>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// 获取或设置发件人显示名称。
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 获取或设置发件人邮箱地址，应为可解析的电子邮件地址。
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// 获取或设置邮件队列空闲轮询间隔，单位为毫秒，默认值为 <c>3000</c>。
    /// </summary>
    public int SleepInterval { get; set; } = 3000;
}