using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Bing.Extensions;
using Bing.Net.Mail.Configs;
using Bing.Net.Mail.Core;

namespace Bing.Net.Mail.Smtp
{
    /// <summary>
    /// 基于SMTP的电子邮件发送器
    /// </summary>
    public class SmtpEmailSender : EmailSenderBase, ISmtpEmailSender
    {
        /// <summary>
        /// 电子邮件配置提供器
        /// </summary>
        private readonly IEmailConfigProvider _configProvider;

        /// <summary>
        /// 初始化一个<see cref="SmtpEmailSender"/>类型的实例
        /// </summary>
        /// <param name="provider">电子邮件配置提供器</param>
        public SmtpEmailSender(IEmailConfigProvider provider) : base(provider)
        {
            _configProvider = provider;
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        protected override void SendEmail(MailMessage mail)
        {
            using (var smtpClient = BuildClient())
            {
                smtpClient.Send(mail);
            }
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mail">邮件</param>
        protected override async Task SendEmailAsync(MailMessage mail)
        {
            using (var smtpClient = BuildClient())
            {
                await smtpClient.SendMailAsync(mail);
            }
        }

        /// <summary>
        /// 生成SMTP客户端
        /// </summary>
        /// <returns></returns>
        public SmtpClient BuildClient()
        {
            var config = _configProvider.GetConfig();
            var host = config.Host;
            var port = config.Port;

            var smtpClient = new SmtpClient(host, port);
            try
            {
                if (config.EnableSsl)
                {
                    smtpClient.EnableSsl = true;
                }

                if (config.UseDefaultCredentials)
                {
                    smtpClient.UseDefaultCredentials = true;
                }
                else
                {
                    smtpClient.UseDefaultCredentials = false;
                    var userName = config.UserName;
                    if (!userName.IsEmpty())
                    {
                        var password = config.Password;
                        var domain = config.Domain;
                        smtpClient.Credentials = !domain.IsEmpty()
                            ? new NetworkCredential(userName, password, domain)
                            : new NetworkCredential(userName, password);
                    }
                }

                return smtpClient;
            }
            catch
            {
                smtpClient.Dispose();
                throw;
            }
        }
    }
}
