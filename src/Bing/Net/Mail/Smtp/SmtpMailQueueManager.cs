using System;
using Bing.Logs;
using Bing.Net.Mail.Abstractions;
using Bing.Net.Mail.Configs;
using Bing.Net.Mail.Core;

namespace Bing.Net.Mail.Smtp
{
    /// <summary>
    /// 基于SMTP的邮件队列管理器
    /// </summary>
    public class SmtpMailQueueManager:MailQueueManagerBase,IMailQueueManager
    {
        /// <summary>
        /// SMTP电子邮件发送器
        /// </summary>
        private readonly ISmtpEmailSender _smtpEmailSender;

        /// <summary>
        /// 初始化一个<see cref="SmtpMailQueueManager"/>类型的实例
        /// </summary>
        /// <param name="emailConfigProvider">电子邮件配置提供器</param>
        /// <param name="mailQueueProvider">邮件队列提供程序</param>
        /// <param name="smtpEmailSender">SMTP电子邮件发送器</param>
        public SmtpMailQueueManager(IEmailConfigProvider emailConfigProvider, IMailQueueProvider mailQueueProvider,ISmtpEmailSender smtpEmailSender) : base(emailConfigProvider, mailQueueProvider)
        {
            _smtpEmailSender = smtpEmailSender;
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">电子邮件</param>
        protected override void SendMail(EmailBox box)
        {
            _smtpEmailSender.Send(box);
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="log">日志</param>
        /// <param name="level">日志等级</param>
        protected override void WriteLog(string log, LogLevel level)
        {
            Console.WriteLine(log);
        }
    }
}
