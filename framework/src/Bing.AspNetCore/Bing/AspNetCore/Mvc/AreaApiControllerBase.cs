using Bing.Logs;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// WebApi的区域控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[area]/[controller]/[action]")]
    public abstract class AreaApiControllerBase : BingControllerBase
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        public ILog Log => _log ??= GetLog();

        /// <summary>
        /// 获取日志操作
        /// </summary>
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
