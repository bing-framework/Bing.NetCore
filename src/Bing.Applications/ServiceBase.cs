using Bing.Logs;
using Bing.Sessions;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
    public abstract class ServiceBase : IService
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        public ILog Log => _log ?? (_log = GetLog());

        /// <summary>
        /// 用户会话
        /// </summary>
        public virtual ISession Session => Bing.Sessions.Session.Instance;

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        protected virtual ILog GetLog()
        {
            try
            {
                return Bing.Logs.Log.GetLog(this);
            }
            catch
            {
                return Bing.Logs.Log.Null;
            }
        }
    }
}
