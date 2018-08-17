using System;
using Bing.Logs;
using Bing.Net.Mail.Abstractions;
using Bing.Net.Mail.Configs;
using Bing.Net.Mail.Core;

namespace Bing.MailKit
{
    /// <summary>
    /// Mailkit邮件队列管理器
    /// </summary>
    public class MailKitMailQueueManager:MailQueueManagerBase,IMailQueueManager
    {
        /// <summary>
        /// MailKit电子邮件发送器
        /// </summary>
        private readonly IMailKitEmailSender _mailKitEmailSender;

        /// <summary>
        /// 初始化一个<see cref="MailKitMailQueueManager"/>类型的实例
        /// </summary>
        /// <param name="emailConfigProvider">电子邮件配置提供器</param>
        /// <param name="mailQueueProvider">邮件队列提供程序</param>
        /// <param name="mailKitEmailSender">MailKit电子邮件发送器</param>
        public MailKitMailQueueManager(IEmailConfigProvider emailConfigProvider, IMailQueueProvider mailQueueProvider,IMailKitEmailSender mailKitEmailSender) : base(emailConfigProvider, mailQueueProvider)
        {
            _mailKitEmailSender = mailKitEmailSender;
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="box">电子邮件</param>
        protected override void SendMail(EmailBox box)
        {
            _mailKitEmailSender.Send(box);
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
