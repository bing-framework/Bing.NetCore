using System;
using System.Diagnostics;
using Bing.Aspects;
using Bing.DependencyInjection;
using Bing.Logs;
using Bing.Logs.Abstractions;

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
        protected ILog Logger { get; set; }

        public DebugLogJob(ILogContext logContext)
        {
            LogContext = logContext;
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        public void WriteLog()
        {
            Debug.WriteLine($"【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志 | {LogContext.TraceId} | {LogContext.LogId}");
            var log = GetLog();
            log.Class(GetType().FullName)
                .Caption("DebugLogJob")
                .Content($"【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志 | {LogContext.TraceId} | {LogContext.LogId}")
                .Trace();

            Logger.Class(GetType().FullName)
                .Caption("DebugLogJob")
                .Content($"【{DateTime.Now:yyyy-MM-dd HH:mm:ss.sss}】 {nameof(WriteLog)} | 写入日志 | {LogContext.TraceId} | {LogContext.LogId}")
                .Trace();
        }

        private ILog GetLog()
        {
            try
            {
                return Log.GetLog("HangfireLog");
            }
            catch
            {
                return Log.Null;
            }
        }
    }
}
