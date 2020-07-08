using System;
using System.Threading.Tasks;
using Bing.Exceptions;
using Bing.Logs;
using Bing.Webs.Controllers;
using Exceptionless;
using Microsoft.AspNetCore.Mvc;
using LogLevel = Exceptionless.Logging.LogLevel;

namespace Bing.Samples.Api.Controllers
{
    /// <summary>
    /// 日志测试控制器
    /// </summary>
    [Route("api/[controller]/[action]")]
    public class LogTestController : ApiControllerBase
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
        public async Task WriteNotImplementedExceptionLog(int type)
        {
            switch (type)
            {
                case 1:
                    throw new NotImplementedException("尚未实现这个方法");
                case 2:
                    throw new ArgumentNullException(nameof(type));
                case 3:
                    throw new ArgumentException(nameof(type));
                default:
                    throw new OutOfMemoryException(nameof(type));
            }
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
                e.Data["test"] = "隔壁老王666";
                e.Data["test1"] = new {T1 = 444, T2 = "gebi "};
                Log.Caption("全局异常捕获")
                    .Content(e.Message)
                    .Exception(e);
                e.Log(Log);
            }
        }

        /// <summary>
        /// 写入多个日志
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task WriteMoreInfo()
        {
            Log.Caption("测试日志1").Content("测试一下内容1").Info();
            Log.Caption("测试日志2").Content("测试一下内容2").Trace();
        }

        /// <summary>
        /// Exceptionless 多日志写入
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        public async Task ExceptionlessMoreInfo()
        {
            ExceptionlessClient _client = ExceptionlessClient.Default;
            _client.CreateLog("测试日志1", LogLevel.Info).Submit();
            _client.CreateLog("测试日志2", LogLevel.Trace).Submit();
        }

        [HttpGet("testGuid")]
        public async Task TestGuid(Guid id)
        {
            var result = id;
            throw new Warning("测试一下异常");
        }
    }
}
