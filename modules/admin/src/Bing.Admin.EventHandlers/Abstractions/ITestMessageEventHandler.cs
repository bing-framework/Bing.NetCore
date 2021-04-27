using System.Threading.Tasks;
using Bing.Admin.Systems.Domain.Events;
using DotNetCore.CAP;

namespace Bing.Admin.EventHandlers.Abstractions
{
    /// <summary>
    /// 测试消息事件处理器
    /// </summary>
    public interface ITestMessageEventHandler
    {
        /// <summary>
        /// 测试消息
        /// </summary>
        /// <param name="message">消息</param>
        Task TestMessage1Async(TestMessage message, [FromCap] CapHeader header);

        /// <summary>
        /// 测试消息
        /// </summary>
        /// <param name="message">消息</param>
        Task TestMessage2Async(TestMessage message);
    }
}
