using Bing.Emailing;
using Bing.MailKit.Configs;

namespace Bing.MailKit.Extensions;

/// <summary>
/// 聚合邮件内容和 MailKit 传输层配置。
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// 获取或设置邮件主题、发件人及收件人等邮件内容配置。
    /// </summary>
    public EmailConfig EmailConfig { get; set; } = new EmailConfig();

    /// <summary>
    /// 获取或设置 MailKit SMTP 连接和传输配置。
    /// </summary>
    public MailKitConfig MailKitConfig { get; set; } = new MailKitConfig();
}