using System;
using System.Collections.Generic;
using System.Text;
using Bing.EmailCenter.Core;

namespace Bing.EmailCenter.Abstractions
{
    /// <summary>
    /// 邮件队列提供程序
    /// 参考：https://www.cnblogs.com/rocketRobin/p/9294845.html
    /// </summary>
    public interface IMailQueueProvider
    {
        /// <summary>
        /// 队列剩余邮件数量
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 队列是否为空
        /// </summary>
        bool IsEmpty { get; set; }

        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="box">邮件</param>
        void Enqueue(MailBox box);

        /// <summary>
        /// 尝试出队，获取邮件
        /// </summary>
        /// <param name="box">邮件</param>
        /// <returns></returns>
        bool TryDequeue(out MailBox box);

        
    }
}
