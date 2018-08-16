using System.Net.Mail;
using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Smtp
{
    /// <summary>
    /// 基于SMTP的电子邮件发送器
    /// </summary>
    public interface ISmtpEmailSender:IEmailSender
    {
        /// <summary>
        /// 创建客户端
        /// </summary>
        /// <returns></returns>
        SmtpClient BuildClient();
    }
}
