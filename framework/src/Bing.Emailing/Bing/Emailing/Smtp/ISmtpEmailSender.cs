using System.Net.Mail;

namespace Bing.Emailing.Smtp;

/// <summary>
/// 定义可创建 SMTP 客户端的邮件发送器。
/// </summary>
public interface ISmtpEmailSender : IEmailSender
{
    /// <summary>
    /// 根据当前邮件配置创建 SMTP 客户端。
    /// </summary>
    /// <returns>已应用当前连接和认证配置的 SMTP 客户端。</returns>
    /// <remarks>调用方负责释放返回的客户端。</remarks>
    SmtpClient BuildClient();
}