using System.Threading.Tasks;
using Bing.Logs;
using Bing.Logs.Abstractions;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 日志工厂
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class LogFactoryController : ApiControllerBase
    {
        /// <summary>
        /// 日志工厂
        /// </summary>
        private readonly ILogFactory _logFactory;

        /// <summary>
        /// 初始化一个<see cref="LogFactoryController"/>类型的实例
        /// </summary>
        /// <param name="logFactory">日志工厂</param>
        public LogFactoryController(ILogFactory logFactory)
        {
            _logFactory = logFactory;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="content">内容</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<string> WriteLog(string content)
        {
            var nlog = _logFactory.GetLog(LogConst.DefaultNLogName);
            nlog.Class(this.GetType().FullName).Caption("NLog日志").Content(content).Info();

            var log4Net = _logFactory.GetLog(LogConst.DefaultLog4NetName);
            log4Net.Class(this.GetType().FullName).Caption("Log4net日志").Content(content).Info();

            var serilog = _logFactory.GetLog(LogConst.DefaultSerilogName);
            serilog.Class(this.GetType().FullName).Caption("Serilog日志").Content(content).Info();

            var exceptionless = _logFactory.GetLog(LogConst.DefaultExceptionlessName);
            exceptionless.Class(this.GetType().FullName).Caption("exceptionless日志").Content(content).Info();

            return await Task.FromResult(content);
        }
    }
}
