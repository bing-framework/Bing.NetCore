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
    /// Serilog日志测试控制器
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class SerilogLogTestController:ApiControllerBase
    {
        /// <summary>
        /// 日志
        /// </summary>
        private ILog Log { get; }

        /// <summary>
        /// 初始化一个<see cref="SerilogLogTestController"/>类型的实例
        /// </summary>
        public SerilogLogTestController(ILog log)
        {
            Log = log;
        }

        /// <summary>
        /// 写入自定日志-跟踪
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeTraceLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Trace();
            return await Task.FromResult(content);
        }

        /// <summary>
        /// 写入自定日志-调试
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeDebugLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Debug();
            return await Task.FromResult(content);
        }

        /// <summary>
        /// 写入自定日志-信息
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeInfoLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Info();
            return await Task.FromResult(content);
        }

        /// <summary>
        /// 写入自定日志-警告
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeWarnLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Warn();
            return await Task.FromResult(content);
        }

        /// <summary>
        /// 写入自定日志-错误
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeErrorLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Error();
            return await Task.FromResult(content);
        }

        /// <summary>
        /// 写入自定日志-致命错误
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteCustomeFatalLog(string content)
        {
            Log.Class(typeof(SerilogLogTestController).FullName).Content(content).Fatal();
            return await Task.FromResult(content);
        }
    }
}
