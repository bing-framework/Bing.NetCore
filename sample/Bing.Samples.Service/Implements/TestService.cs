using System;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Events.Messages;
using Bing.Logs.Aspects;
using Bing.Logs.Extensions;
using Bing.Samples.Domain.Events;
using Bing.Samples.Service.Abstractions;

namespace Bing.Samples.Service.Implements
{
    /// <summary>
    /// 测试服务
    /// </summary>
    public class TestService : ServiceBase, ITestService
    {
        /// <summary>
        /// 初始化一个<see cref="TestService"/>类型的实例
        /// </summary>
        public TestService(IMessageEventBus messageEventBus)
        {
            MessageEventBus = messageEventBus;
        }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        protected IMessageEventBus MessageEventBus { get; set; }

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="id">标识</param>
        [DebugLog]
        public Task<string> GetAsync(Guid id) => Task.FromResult(id.ToString());

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="content">内容</param>
        public async Task WriteLogAsync(string content)
        {
            Log.Caption("服务写入日志")
                .Content(content)
                .Trace();
            var message = new LogMessage() {Content = content};
            await MessageEventBus.PublishAsync(message.ToEvent());
        }
    }
}
