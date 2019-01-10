using Bing.Logs;
using Bing.Sessions;
using Bing.Webs.Commons;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers
{
    /// <summary>
    /// WebApi控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    [ErrorLog]
    [TraceLog]
    public abstract class ApiControllerBase:Controller
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog _log;

        /// <summary>
        /// 日志
        /// </summary>
        public virtual ILog Log => _log ?? (_log = GetLog());

        /// <summary>
        /// 会话
        /// </summary>
        public virtual ISession Session => Security.Sessions.Session.Instance;

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        private ILog GetLog()
        {
            try
            {
                return Logs.Log.GetLog(this);
            }
            catch
            {
                return Logs.Log.Null;
            }
        }

        /// <summary>
        /// 返回成功消息
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="message">消息</param>
        /// <returns></returns>
        protected virtual IActionResult Success(dynamic data = null, string message = null)
        {
            if (message == null)
            {
                message = Bing.Properties.R.Success;
            }

            return new Result(StateCode.Ok, message, data);
        }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <param name="message">消息</param>
        /// <returns></returns>
        protected IActionResult Fail(string message)
        {
            return new Result(StateCode.Fail,message);
        }
    }
}
