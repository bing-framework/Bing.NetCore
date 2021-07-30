using System.Collections.Generic;
using Bing.Canal.Model;

namespace Bing.Canal.Server.Models
{
    /// <summary>
    /// Canal内容体
    /// </summary>
    public class CanalBody : INotification
    {
        /// <summary>
        /// 批次标识
        /// </summary>
        public long BatchId { get; }

        /// <summary>
        /// 消息
        /// </summary>
        public IList<DataChange> Message { get; }

        /// <summary>
        /// 初始化一个<see cref="CanalBody"/>类型的实例
        /// </summary>
        public CanalBody() { }

        /// <summary>
        /// 初始化一个<see cref="CanalBody"/>类型的实例
        /// </summary>
        /// <param name="message">消息</param>
        /// <param name="batchId">批次标识</param>
        public CanalBody(IList<DataChange> message, long batchId)
        {
            Message = message;
            BatchId = batchId;
        }

    }
}
