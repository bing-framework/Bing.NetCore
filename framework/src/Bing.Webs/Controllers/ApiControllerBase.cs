using System;
using Bing.Logs;
using Bing.Sessions;
using Bing.Webs.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Webs.Controllers
{
    /// <summary>
    /// WebApi控制器基类
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Obsolete("请使用Bing.AspNetCore.Mvc.ApiControllerBase")]
    public abstract class ApiControllerBase : Controller
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
        /// 会话
        /// </summary>
        protected virtual ISession Session => HttpContext.RequestServices.GetService<ISession>();

        /// <summary>
        /// 获取日志操作
        /// </summary>
        protected virtual ILog GetLog()
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
        protected virtual IActionResult Success(dynamic data = null, string message = null)
        {
            if (message == null)
                message = Bing.Properties.R.Success;
            return new Result(StateCode.Ok, message, data);
        }

        /// <summary>
        /// 返回失败消息
        /// </summary>
        /// <param name="message">消息</param>
        protected IActionResult Fail(string message) => new Result(StateCode.Fail, message);
    }
}
