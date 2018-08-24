using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Extensions;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 日志测试控制器
    /// </summary>
    public class LogTestController:ApiControllerBase
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog Log { get; set; }

        /// <summary>
        /// 初始化一个<see cref="LogTestController"/>类型的实例
        /// </summary>
        public LogTestController(ILog log)
        {
            Log = log;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="content">内容</param>
        [HttpPost]
        public async Task<string> WriteLog(string content)
        {
            Log.Class(typeof(LogTestController).FullName).Content(content).Info();
            return content;
        }

        /// <summary>
        /// 写入未实现异常日志
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task WriteNotImplementedExceptionLog()
        {
            throw new NotImplementedException("尚未实现这个方法");
        }

        /// <summary>
        /// 写入除零异常
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task WriteZeroExceptionLog()
        {
            try
            {
                var a = 1;
                var b = 0;
                var result = a / b;
            }
            catch (Exception e)
            {
                Log.Caption("全局异常捕获").Error(e.Message);
                e.Log(Log);
            }
            
        }
    }
}
