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
    SmtpClient Build();
}