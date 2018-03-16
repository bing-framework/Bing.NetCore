using System;
using System.Collections.Generic;
using System.Text;
using Bing.Properties;
using Bing.Webs.Commons;
using Bing.Webs.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Webs.Controllers
{
    /// <summary>
    /// WebApi控制器
    /// </summary>
    [Route("api/[controller]")]
    [ExceptionHandler]
    public class WebApiControllerBase:Controller
    {
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
