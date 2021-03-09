using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.EventHandlers.Abstractions;
using Bing.Admin.Infrastructure;
using Bing.Admin.Systems.Domain.Events;
using Bing.Events;
using Bing.Events.Messages;

namespace Bing.Admin.EventHandlers.Implements
{
    /// <summary>
    /// 测试消息事件处理器
    /// </summary>
    public class TestMessageEventHandler : MessageEventHandlerBase, ITestMessageEventHandler
    {
        /// <summary>
        /// 初始化一个<see cref="TestMessageEventHandler"/>类型的实例
        /// </summary>
        public TestMessageEventHandler(IAdminUnitOfWork unitOfWork, IMessageEventBus messageEventBus)
        {
            UnitOfWork = unitOfWork;
            MessageEventBus = messageEventBus;
        }

        /// <summary>
        /// 工作单元
        /// </summary>
        protected IAdminUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        protected IMessageEventBus MessageEventBus { get; }

        /// <summary>
        /// 测试消息
        /// </summary>
        /// <param name="message">消息</param>
        [EventHandler(MessageEventConst.TestMessage1)]
        public async Task TestMessage1Async(TestMessage message)
        {
            if(message.ThrowException)
                throw new NotImplementedException("主动触发，暂未生效");
            Debug.WriteLine(message.Id);
            await MessageEventBus.PublishAsync(new TestMessageEvent2(message, message.Send));
            if(message.NeedCommit)
                await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 测试消息
        /// </summary>
        /// <param name="message">消息</param>
        [EventHandler(MessageEventConst.TestMessage2)]
        public Task TestMessage2Async(TestMessage message)
        {
            Debug.WriteLine(message.Id);
            return Task.CompletedTask;
        }
    }
}
