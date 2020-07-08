using System;
using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Bing.Logs.NLog.Internal;
using NLogs = NLog;

namespace Bing.Logs.NLog
{
    /// <summary>
    /// NLog 日志提供程序
    /// </summary>
    public class NLogProvider : ILogProvider
    {
        #region 属性

        /// <summary>
        /// NLog 日志操作
        /// </summary>
        private readonly NLogs.ILogger _logger;

        /// <summary>
        /// 日志格式化器
        /// </summary>
        private readonly ILogFormat _format;

        /// <summary>
        /// 日志名称
        /// </summary>
        public string LogName => _logger.Name;

        /// <summary>
        /// 调试级别是否启用
        /// </summary>
        public bool IsDebugEnabled => _logger.IsDebugEnabled;

        /// <summary>
        /// 跟踪级别是否启用
        /// </summary>
        public bool IsTraceEnabled => _logger.IsTraceEnabled;

        /// <summary>
        /// 是否分布式日志
        /// </summary>
        public bool IsDistributedLog => false;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="NLogProvider"/>类型的实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="format">日志格式化器</param>
        public NLogProvider(string logName, ILogFormat format = null)
        {
            _logger = GetLogger(logName);
            _format = format;
        }

        #endregion

        /// <summary>
        /// 获取NLog日志操作
        /// </summary>
        /// <param name="logName">日志名称</param>
        public static NLogs.ILogger GetLogger(string logName) => NLogs.LogManager.GetLogger(logName);

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="level">日志等级</param>
        /// <param name="content">日志内容</param>
        public void WriteLog(LogLevel level, ILogContent content)
        {
            var provider = GetFormatProvider();
            if (provider == null)
            {
                _logger.Log(LogLevelSwitcher.Switch(level), content);
                return;
            }
            _logger.Log(LogLevelSwitcher.Switch(level), provider, content);
        }

        /// <summary>
        /// 获取格式化提供程序
        /// </summary>
        private IFormatProvider GetFormatProvider() => _format == null ? null : new FormatProvider(_format);
    }
}
