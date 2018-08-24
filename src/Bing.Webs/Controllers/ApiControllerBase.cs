using System;
using System.Collections.Generic;
using System.Text;
using Bing.Helpers;
using Bing.Logs.Aspects;
using Bing.Properties;
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
    //[ExceptionHandler]
    [ErrorLog]
    public class ApiControllerBase:Controller
    {
        /// <summary>
        /// 会话
        /// </summary>
        private readonly ISession _session;

        /// <summary>
        /// 会话
        /// </summary>
        public virtual ISession Session => _session ?? NullSession.Instance;

        /// <summary>
        /// 初始化一个<see cref="ApiControllerBase"/>类型的实例
        /// </summary>
        public ApiControllerBase()
        {
            _session = Ioc.Create<ISession>();
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
