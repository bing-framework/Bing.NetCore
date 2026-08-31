using MailKit.Net.Smtp;

namespace Bing.MailKit;

/// <summary>
/// MailKit SMTP生成器
/// </summary>
public interface IMailKitSmtpBuilder
{
    /// <summary>
    /// 生成SMTP客户端
    /// </summary>
    /// <returns>已配置的 SMTP 客户端。</returns>
    SmtpClient Build();
}