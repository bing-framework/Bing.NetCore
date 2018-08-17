using Bing.Net.Mail.Abstractions;

namespace Bing.Net.Mail.Core
{
    /// <summary>
    /// 邮件队列服务
    /// </summary>
    public class MailQueueService:IMailQueueService
    {
        /// <summary>
        /// 邮件队列提供程序
        /// </summary>
        private readonly IMailQueueProvider _provider;

        /// <summary>
        /// 初始化一个<see cref="MailQueueService"/>类型的实例
        /// </summary>
        /// <param name="provider">邮件队列提供程序</param>
        public MailQueueService(IMailQueueProvider provider)
        {
            _provider = provider;
        }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="box">电子邮件</param>
        public void Enqueue(EmailBox box)
        {
            _provider.Enqueue(box);
        }
    }
}
