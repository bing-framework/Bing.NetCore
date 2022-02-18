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
using Bing.Events.Cap;
using Bing.Events.Messages;
using DotNetCore.CAP;
using Microsoft.Extensions.Logging;

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
        public TestMessageEventHandler(IAdminUnitOfWork unitOfWork, IMessageEventBus messageEventBus, ILogger<TestMessageEventHandler> logger)
        {
            UnitOfWork = unitOfWork;
            MessageEventBus = messageEventBus;
            Logger = logger;
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
        /// 日志
        /// </summary>
        protected ILogger<TestMessageEventHandler> Logger { get; }

        /// <summary>
        /// 测试消息
        /// </summary>
        //[CapTrace]
        [EventHandler(MessageEventConst.TestMessage1)]
        public async Task TestMessage1Async(TestMessage message, [FromCap] CapHeader header)
        {
            if (message.ThrowException)
                throw new NotImplementedException("主动触发，暂未生效");
            Log.Info($"测试一波CAP消息 - 0: {message.Id}");
            Logger.LogDebug($"测试一波CAP消息 - 1: {message.Id}");
            var log = Bing.Logs.Log.GetLog(nameof(TestMessageEventHandler));
            log.Debug($"测试一波CAP消息 - 1 - 1: {message.Id}");
            Debug.WriteLine(message.Id);
            await MessageEventBus.PublishAsync(new TestMessageEvent2(message, message.Send));
            if (message.NeedCommit)
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
            Logger.LogDebug($"测试一波CAP消息 - 2: {message.Id}");
            var log = Bing.Logs.Log.GetLog(nameof(TestMessageEventHandler));
            log.Debug($"测试一波CAP消息 - 2 - 1: {message.Id}");
            return Task.CompletedTask;
        }
    }
}
