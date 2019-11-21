using System.Net.Mail;
using System.Threading.Tasks;
using Bing.MailKit.Extensions;
using Bing.Net.Mail.Configs;
using Bing.Net.Mail.Core;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace Bing.MailKit
{
    /// <summary>
    /// MailKit 电子邮件发送器
    /// </summary>
    public class MailKitEmailSender : EmailSenderBase, IMailKitEmailSender
    {
        /// <summary>
        /// SMTP 生成器
        /// </summary>
        private readonly IMailKitSmtpBuilder _smtpBuilder;

        /// <summary>
        /// 初始化一个<see cref="MailKitEmailSender"/>类型的实例
        /// </summary>
        /// <param name="provider">电子邮件配置提供器</param>
        /// <param name="smtpBuilder">SMTP 生成器</param>
        public MailKitEmailSender(IEmailConfigProvider provider, IMailKitSmtpBuilder smtpBuilder) : base(provider)
        {
            _smtpBuilder = smtpBuilder;
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        protected override void SendEmail(MailMessage mail)
        {
            using (var client = BuildSmtpClient())
            {
                var message = mail.ToMimeMessage();
                client.Send(message);
                client.Disconnect(true);
            }
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        protected override async Task SendEmailAsync(MailMessage mail)
        {
            using (var client = BuildSmtpClient())
            {
                var message = mail.ToMimeMessage();
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
        }

        /// <summary>
        /// 生成SMTP客户端
        /// </summary>
        protected virtual SmtpClient BuildSmtpClient() => _smtpBuilder.Build();
    }
}
