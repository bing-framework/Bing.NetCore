using System;
using System.Threading.Tasks;
using Bing.Datas.Transactions;
using Bing.Events.Messages;
using Bing.Logs;
using Bing.Logs.Extensions;
using DotNetCore.CAP;

namespace Bing.Events.Cap
{
    /// <summary>
    /// Cap消息事件总线
    /// </summary>
    public class MessageEventBus : IMessageEventBus
    {
        /// <summary>
        /// 事件发布器
        /// </summary>
        public ICapPublisher Publisher { get; set; }

        /// <summary>
        /// 事务操作管理器
        /// </summary>
        public ITransactionActionManager TransactionActionManager { get; set; }

        /// <summary>
        /// 初始化一个<see cref="MessageEventBus"/>类型的实例
        /// </summary>
        /// <param name="publisher">事件发布器</param>
        /// <param name="transactionActionManager">事务操作管理器</param>
        public MessageEventBus(ICapPublisher publisher, ITransactionActionManager transactionActionManager)
        {
            Publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            TransactionActionManager = transactionActionManager ?? throw new ArgumentNullException(nameof(transactionActionManager));
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="event">事件</param>
        public Task PublishAsync<TEvent>(TEvent @event) where TEvent : IMessageEvent
        {
            return PublishAsync(@event.Name, @event.Data, @event.Callback, @event.Send);
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="name">消息名称</param>
        /// <param name="data">事件数据</param>
        /// <param name="callback">回调名称</param>
        /// <param name="send">是否立即发送消息</param>
        /// <returns></returns>
        public async Task PublishAsync(string name, object data, string callback = null, bool send = false)
        {
            if (send)
            {
                Publisher.Transaction.AutoCommit = true;
                await InternalPublishAsync(name, data, callback);
                return;
            }
            TransactionActionManager.Register(async transaction =>
            {
                Publisher.Transaction.DbTransaction = transaction;
                Publisher.Transaction.AutoCommit = false;
                await InternalPublishAsync(name, data, callback);
            });
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="name">消息名称</param>
        /// <param name="data">事件数据</param>
        /// <param name="callback">回调名称</param>
        /// <returns></returns>
        private async Task InternalPublishAsync(string name, object data, string callback)
        {
            await Publisher.PublishAsync(name, data, callback);
            WriteLog(name);
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="name">消息名称</param>
        private void WriteLog(string name)
        {
            var log = GetLog();
            if (log.IsDebugEnabled == false)
            {
                return;
            }

            log.Caption("Cap已发送事件")
                .Content($"消息名称:{name}")
                .Debug();
        }

        /// <summary>
        /// 获取日志
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Log.GetLog(this);
            }
            catch
            {
                return Log.Null;
            }
        }
    }
}
