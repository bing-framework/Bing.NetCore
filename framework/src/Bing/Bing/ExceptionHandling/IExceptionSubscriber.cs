using System.Threading.Tasks;

namespace Bing.ExceptionHandling
{
    /// <summary>
    /// 异常订阅器
    /// </summary>
    public interface IExceptionSubscriber
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="context">异常通知上下文</param>
        Task HandleAsync(ExceptionNotificationContext context);
    }
}
