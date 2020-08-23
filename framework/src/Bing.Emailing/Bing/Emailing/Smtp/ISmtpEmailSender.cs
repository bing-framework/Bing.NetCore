using System.Net.Mail;

namespace Bing.Emailing.Smtp
{
    /// <summary>
    /// 基于SMTP的电子邮件发送器
    /// </summary>
    public interface ISmtpEmailSender : IEmailSender
    {
        /// <summary>
        /// 生成SMTP客户端
        /// </summary>
        SmtpClient BuildClient();
    }
}
