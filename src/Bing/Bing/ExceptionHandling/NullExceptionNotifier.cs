using System.Threading.Tasks;

namespace Bing.ExceptionHandling
{
    /// <summary>
    /// 空异常通知器
    /// </summary>
    public class NullExceptionNotifier : IExceptionNotifier
    {
        /// <summary>
        /// 实例
        /// </summary>
        public static IExceptionNotifier Instance { get; } = new NullExceptionNotifier();

        /// <summary>
        /// 初始化一个<see cref="NullExceptionNotifier"/>类型的实例
        /// </summary>
        private NullExceptionNotifier()
        {
        }

        /// <summary>
        /// 通知
        /// </summary>
        /// <param name="context">异常通知上下文</param>
        public Task NotifyAsync(ExceptionNotificationContext context) => Task.CompletedTask;
    }
}
