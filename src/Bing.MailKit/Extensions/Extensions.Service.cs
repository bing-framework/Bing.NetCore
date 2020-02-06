using System;
using Bing.MailKit.Configs;
using Bing.Net.Mail.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.MailKit.Extensions
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
        public static void AddMailKit(this IServiceCollection services, Action<EmailOptions> setupAcion)
        {
            var options = new EmailOptions();
            setupAcion?.Invoke(options);
            services.TryAddSingleton<IEmailConfigProvider>(new DefaultEmailConfigProvider(options.EmailConfig));
            services.TryAddSingleton<IMailKitConfigProvider>(new DefaultMailKitConfigProvider(options.MailKitConfig));
            services.TryAddScoped<IMailKitSmtpBuilder, DefaultMailKitSmtpBuilder>();
            services.TryAddScoped<IMailKitEmailSender, MailKitEmailSender>();
        }

        /// <summary>
        /// 注册MailKit邮件操作
        /// </summary>
        /// <typeparam name="TEmailConfigProvider">邮件配置提供器</typeparam>
        /// <typeparam name="TMailKitConfigProvider">MailKit配置提供器</typeparam>
        /// <param name="services">服务集合</param>
        public static void AddMailKit<TEmailConfigProvider, TMailKitConfigProvider>(this IServiceCollection services)
            where TEmailConfigProvider : class, IEmailConfigProvider
            where TMailKitConfigProvider : class, IMailKitConfigProvider
        {
            services.TryAddScoped<IEmailConfigProvider, TEmailConfigProvider>();
            services.TryAddScoped<IMailKitConfigProvider, TMailKitConfigProvider>();
            services.TryAddScoped<IMailKitSmtpBuilder, DefaultMailKitSmtpBuilder>();
            services.TryAddScoped<IMailKitEmailSender, MailKitEmailSender>();
        }
    }
}
