using System;
using System.Diagnostics;
using System.Net;
using Bing.Logs.Abstractions;
using Bing.Logs.Internal;
using Bing.Tracing;

namespace Bing.Logs.Core
{
    /// <summary>
    /// 日志上下文
    /// </summary>
    public class LogContext : ILogContext
    {
        #region 属性

        private int _orderId = 0;

        /// <summary>
        /// 日志标识
        /// </summary>
        public string LogId => GetInfo().IsWebEnv ? $"{GetInfo().TraceId}-{_orderId++}" : GetInfo().GetLogId();

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId
        {
            get
            {
                return (TraceIdContext.Current ??= new TraceIdContext(string.Empty)).TraceId;
            }
        }

        /// <summary>
        /// 计时器
        /// </summary>
        public Stopwatch Stopwatch => GetInfo().Stopwatch;

        /// <summary>
        /// IP
        /// </summary>
        public string Ip => GetInfo().Ip;

        /// <summary>
        /// 主机
        /// </summary>
        public string Host => GetInfo().Host;

        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser => GetInfo().Browser;

        /// <summary>
        /// 请求地址
        /// </summary>
        public string Url => GetInfo().Url;

        #endregion

        /// <summary>
        /// 获取日志上下文信息
        /// </summary>
        private LogContextInfo GetInfo()
        {
            return LogContextInfo.Current ?? (LogContextInfo.Current = CreateInfo());
        }

        /// <summary>
        /// 创建日志上下文信息
        /// </summary>
        protected virtual LogContextInfo CreateInfo() =>
            new LogContextInfo
            {
                TraceId = GetTraceId(),
                Stopwatch = GetStopwatch(),
                Host = Dns.GetHostName(),
            };

        /// <summary>
        /// 获取跟踪号
        /// </summary>
        protected virtual string GetTraceId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// 获取计时器
        /// </summary>
        protected Stopwatch GetStopwatch()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            return stopwatch;
        }
    }
}
