using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EmailCenter.Abstractions
{
    /// <summary>
    /// 邮件队列管理器(邮件发送机)
    /// </summary>
    public interface IMailQueueManager
    {
        /// <summary>
        /// 是否运行中
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 队列数
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 运行
        /// </summary>
        void Run();

        /// <summary>
        /// 停止
        /// </summary>
        void Stop();

    }
}
