using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Emailing.Smtp
{
    /// <summary>
    /// 邮件扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册MailKit邮件操作
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="setupAcion">配置操作</param>
        public static void AddSmtpEmail(this IServiceCollection services, Action<EmailConfig> setupAcion)
        {
            var config = new EmailConfig();
            setupAcion?.Invoke(config);
            services.TryAddSingleton<IEmailConfigProvider>(new DefaultEmailConfigProvider(config));
            services.TryAddScoped<ISmtpEmailSender, SmtpEmailSender>();
        }

        /// <summary>
        /// 注册MailKit邮件操作
        /// </summary>
        /// <typeparam name="TEmailConfigProvider">邮件配置提供器</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddMailKit<TEmailConfigProvider>(this IServiceCollection services)
            where TEmailConfigProvider : class, IEmailConfigProvider
        {
            services.TryAddScoped<IEmailConfigProvider, TEmailConfigProvider>();
            services.TryAddScoped<ISmtpEmailSender, SmtpEmailSender>();
        }
    }
}
