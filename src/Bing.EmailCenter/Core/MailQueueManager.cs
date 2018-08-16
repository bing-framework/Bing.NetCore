using System.Net.Mail;
using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Core
{
    /// <summary>
    /// 邮件队列管理器(邮件发送机)
    /// </summary>
    public class MailQueueManager:IMailQueueManager
    {
        /// <summary>
        /// Smtp客户端
        /// </summary>
        private readonly SmtpClient _client;

        public bool IsRunning { get; }
        public int Count { get; }

        public MailQueueManager()
        {
        }

        
        public void Run()
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }
    }
}
