using System;
using System.Collections.Generic;
using System.Text;
using Bing.EmailCenter.Core;

namespace Bing.EmailCenter.Abstractions
{
    /// <summary>
    /// 邮件队列服务
    /// </summary>
    public interface IMailQueueService
    {
        /// <summary>
        /// 入队
        /// </summary>
        /// <param name="box">邮件</param>
        void Enqueue(MailBox box);
    }
}
