using Bing.Logs;
using Microsoft.AspNetCore.Mvc;

namespace Bing.AspNetCore.Mvc
{
    /// <summary>
    /// WebApi 控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ApiControllerBase : BingControllerBase
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        protected ILog Log => _log ??= GetLog();

        
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
