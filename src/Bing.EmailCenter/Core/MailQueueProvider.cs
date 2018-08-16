using System.Collections.Concurrent;
using Bing.EmailCenter.Abstractions;

namespace Bing.EmailCenter.Core
{
    /// <summary>
    /// 邮件队列提供器
    /// </summary>
    public class MailQueueProvider:IMailQueueProvider
    {
        /// <summary>
        /// 线程安全的邮件队列
        /// </summary>
        private static readonly ConcurrentQueue<MailBox> _mailQueue=new ConcurrentQueue<MailBox>();

        /// <summary>
        /// 队列邮件数量
        /// </summary>
        public int Count => _mailQueue.Count;

        /// <summary>
        /// 队列是否为空
        /// </summary>
        public bool IsEmpty => _mailQueue.IsEmpty;

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="box">邮件</param>
        public void Enqueue(MailBox box)
        {
            _mailQueue.Enqueue(box);
        }

        /// <summary>
        /// 尝试出队，获取邮件
        /// </summary>
        /// <param name="box">邮件</param>
        /// <returns></returns>
        public bool TryDequeue(out MailBox box)
        {
            return _mailQueue.TryDequeue(out box);
        }
    }
}
