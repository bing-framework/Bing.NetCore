using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Core
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
        /// <param name="box">邮件</param>
        public void Enqueue(MailBox box)
        {
            _provider.Enqueue(box);
        }
    }
}
