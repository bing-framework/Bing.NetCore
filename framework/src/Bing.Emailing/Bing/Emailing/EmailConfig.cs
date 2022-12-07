namespace Bing.Emailing;

/// <summary>
/// 电子邮件配置
/// </summary>
public class EmailConfig
{
    /// <summary>
    /// 主机
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 端口号
    /// </summary>
    public int Port { get; set; } = 25;

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 域名
    /// </summary>
    public string Domain { get; set; }

    /// <summary>
    /// 是否启用SSL
    /// </summary>
    public bool EnableSsl { get; set; }

    /// <summary>
    /// 是否使用默认证书
    /// </summary>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// 发送地址
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// 睡眠间隔。默认：3秒
    /// </summary>
    public int SleepInterval { get; set; } = 3000;
}