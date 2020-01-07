using System.Threading.Tasks;
using Bing.Samples.Domain.Events;

namespace Bing.Samples.EventHandlers.Abstractions
{
    /// <summary>
    /// 测试 消息事件处理器
    /// </summary>
    public interface ITestMessageEventHandler
    {
        /// <summary>
        /// 创建角色
        /// </summary>
        /// <param name="message">消息</param>
        Task CreateRoleAsync(CreateRoleMessage message);

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="message">消息</param>
        Task WriteLogAsync(LogMessage message);
    }
}
