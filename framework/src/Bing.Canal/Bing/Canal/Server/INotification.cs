using System.Threading.Tasks;

namespace Bing.Canal.Server
{
    /// <summary>
    /// 通知
    /// </summary>
    public interface INotification
    {
    }

    /// <summary>
    /// 通知处理器
    /// </summary>
    /// <typeparam name="TNotification">泛型类型</typeparam>
    public interface INotificationHandler<in TNotification> where TNotification : INotification
    {
        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="notification">对象</param>
        Task HandleAsync(TNotification notification);
    }
}
