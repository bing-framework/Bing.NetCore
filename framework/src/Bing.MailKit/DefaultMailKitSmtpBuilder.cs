using Bing.Emailing;
using Bing.MailKit.Configs;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Bing.MailKit
{
    /// <summary>
    /// 默认MailKit SMTP生成器
    /// </summary>
    public class DefaultMailKitSmtpBuilder : IMailKitSmtpBuilder
    {
        /// <summary>
        /// 电子邮件配置提供器
        /// </summary>
        private readonly IEmailConfigProvider _emailConfigProvider;

        /// <summary>
        /// MailKit配置提供器
        /// </summary>
        private readonly IMailKitConfigProvider _mailKitConfigProvider;

        /// <summary>
        /// 初始化一个<see cref="DefaultMailKitSmtpBuilder"/>类型的实例
        /// </summary>
        /// <param name="emailConfigProvider">电子邮件配置提供器</param>
        /// <param name="mailKitConfigProvider">MailKit配置提供器</param>
        public DefaultMailKitSmtpBuilder(IEmailConfigProvider emailConfigProvider, IMailKitConfigProvider mailKitConfigProvider)
        {
            _emailConfigProvider = emailConfigProvider;
            _mailKitConfigProvider = mailKitConfigProvider;
        }

        /// <summary>
        /// 生成SMTP客户端
        /// </summary>
        public virtual SmtpClient Build()
        {
            var client = new SmtpClient();
            try
            {
                ConfigureClient(client);
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 配置SMTP客户端
        /// </summary>
        /// <param name="client">SMTP客户端</param>
        protected virtual void ConfigureClient(SmtpClient client)
        {
            var emailConfig = this._emailConfigProvider.GetConfig();
            client.Connect(emailConfig.Host, emailConfig.Port, GetSecureSocketOption());
            if (emailConfig.UseDefaultCredentials)
                return;
            client.Authenticate(emailConfig.UserName, emailConfig.Password);
        }

        /// <summary>
        /// 获取安全套接字选项
        /// </summary>
        protected virtual SecureSocketOptions GetSecureSocketOption()
        {
            var config = this._mailKitConfigProvider.GetConfig();
            if (config.SecureSocketOption.HasValue)
                return config.SecureSocketOption.Value;
            var emailConfig = this._emailConfigProvider.GetConfig();
            return emailConfig.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        }
    }
}
