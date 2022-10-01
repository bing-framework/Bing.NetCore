using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logging;
using Bing.Logs;
using Bing.Logs.Abstractions;
using Bing.Utils.Json;
using Exceptionless;
using Microsoft.Extensions.Logging;
using el = global::Exceptionless;
using LogLevel = Exceptionless.Logging.LogLevel;

namespace Bing.Samples.Hangfire.Jobs
{
    public interface IDebugLogJob : IScopedDependency
    {
        /// <summary>
        /// 写入日志
        /// </summary>
        void WriteLog();
    }

    public class DebugLogJob : IDebugLogJob
    {
        /// <summary>
        /// 日志上下文
        /// </summary>
        protected ILogContext LogContext { get; }

        [Autowired]
        protected Bing.Logs.ILog Logger { get; set; }

        protected ILogger<DebugLogJob> SysLogger { get; set; }

        protected ILog<DebugLogJob> CurrentLog { get;  }

        public DebugLogJob(ILogContext logContext, ILogger<DebugLogJob> logger, ILog<DebugLogJob> currentLog)
        {
            LogContext = logContext;
            SysLogger = logger;
            CurrentLog = currentLog;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        public void WriteLog()
        {
            Debug.WriteLine("-------------------------------------------------------------------------------------------------------------------------------------------");
            Debug.WriteLine($"1、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志");
            var log = GetLog();
            //log.Class(GetType().FullName)
            //    .Caption("DebugLogJob")
            //    .Content($"2、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志")
            //    .Debug();
            log.Info($"隔壁老王的信息【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 ");
            el.ExceptionlessClient.Default
                .CreateLog($"隔壁老王的信息-Source【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 ", LogLevel.Info).Submit();
            el.ExceptionlessClient.Default
                .CreateLog($"隔壁老王的信息-Source-NotLevel【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 ").Submit();
            Debug.WriteLine($"CurrentLog: {CurrentLog.GetType()}");
            var id = Guid.NewGuid().ToString();
            using (CurrentLog.BeginScope(new Dictionary<string, object>
            {
                ["UserId"] = "svrooij",
                ["OperationType"] = "update",
            }))
            {
                CurrentLog.Message($"测试日志信息--主动设置--BeginScope {id}").LogInformation();
            }

            CurrentLog.ExtraProperty("test", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.sss"))
                .Message($"测试日志信息--被动设置--BeginScope {id}")
                .Tags(id)
                .LogInformation();

            using (CurrentLog.BeginScope(new Dictionary<string, object>
            {
                ["UserId"] = "svrooij",
                ["OperationType"] = "update",
            }))
            {
                CurrentLog.ExtraProperty("testA7777", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.sss"))
                    .ExtraProperty("Req", new { A = 1, B = "2", C = new { C1 = 1, C2 = id } })
                    .ExtraProperty("ReqJson", JsonHelper.ToJson(new { A = 1, B = "2", C = new { C1 = 1, C2 = id } }))
                    .Message($"测试日志信息--双重设置--BeginScope {id}")
                    .Tags(id)
                    .LogInformation();
            }
            //Logger.Class(GetType().FullName)
            //    .Caption("DebugLogJob")
            //    .Content($"3-1、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志")
            //    .Debug();
            //Logger.Class(GetType().FullName)
            //    .Caption("DebugLogJob")
            //    .Content($"3-2、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志")
            //    .Trace();
            //Logger.Class(GetType().FullName)
            //    .Caption("DebugLogJob")
            //    .Content($"3-3、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志")
            //    .Trace();
            //Logger.Class(GetType().FullName)
            //    .Caption("DebugLogJob")
            //    .Content($"3-4、【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志")
            //    .Trace();

            //SysLogger.LogInformation($"4-1、【系统日志】【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志");
            //SysLogger.LogInformation($"4-2、【系统日志】【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志");
            //SysLogger.LogInformation($"4-3、【系统日志】【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志");
            //SysLogger.LogInformation($"4-4、【系统日志】【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志");
        }

        private Bing.Logs.ILog GetLog()
        {
            try
            {
                return Bing.Logs.Log.GetLog("HangfireLog");
            }
            catch
            {
                return Bing.Logs.Log.Null;
            }
        }
    }
}
