using System;
using System.IO;
using Bing.Extensions;
using Bing.Logs.Abstractions;
using Bing.Logs.Formats;
using Serilog;
using Serilog.Events;

using Serilogs = Serilog;

namespace Bing.Logs.Serilog
{
    /// <summary>
    /// Serilog 日志提供程序
    /// </summary>
    public class SerilogProvider : ILogProvider
    {
        #region 属性

        /// <summary>
        /// Serilog 日志操作
        /// </summary>
        private static Serilogs.ILogger _logger;

        /// <summary>
        /// 日志格式化器
        /// </summary>
        private readonly ILogFormat _format;

        /// <summary>
        /// 日志名称
        /// </summary>
        public string LogName { get; }

        /// <summary>
        /// 调试界别是否启用
        /// </summary>
        public bool IsDebugEnabled => _logger.IsEnabled(LogEventLevel.Debug);

        /// <summary>
        /// 跟踪级别是否启用
        /// </summary>
        public bool IsTraceEnabled => _logger.IsEnabled(LogEventLevel.Verbose);

        /// <summary>
        /// 是否分布式日志
        /// </summary>
        public bool IsDistributedLog => false;

        /// <summary>
        /// Serilog 配置
        /// </summary>
        internal static LoggerConfiguration Configuration { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="SerilogProvider"/>类型的实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="format">日志格式化器</param>
        public SerilogProvider(string logName, ILogFormat format = null)
        {
            LogName = logName;
            _format = format;
        }

        /// <summary>
        /// 初始化Serilog配置
        /// </summary>
        /// <param name="configuration">配置</param>
        internal static void InitConfiguration(LoggerConfiguration configuration = null)
        {
            if (configuration == null)
            {
                Configuration = GetDefaultConfiguration();
            }
            else
            {
                Configuration = configuration;
            }
            _logger = Configuration.CreateLogger();
        }

        /// <summary>
        /// 获取默认配置
        /// </summary>
        /// <returns></returns>
        private static LoggerConfiguration GetDefaultConfiguration()
        {
            var path = $"{AppContext.BaseDirectory}\\logs";
            return new LoggerConfiguration()
                .MinimumLevel.Verbose() // 设置最小记录级别
                .Enrich.FromLogContext()
                // 设置日志输出
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Verbose, path, "Trace"))
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Debug, path, "Debug"))
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Information, path, "Info"))
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Warning, path, "Warn"))
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Error, path, "Error"))
                .WriteTo.Logger(fileLogger => GetOutputConfiguration(fileLogger, LogEventLevel.Fatal, path, "Fatal"));
        }

        private static LoggerConfiguration GetOutputConfiguration(LoggerConfiguration configuration, LogEventLevel level, string path, string name)
        {
            return configuration.Filter.ByIncludingOnly(p => p.Level.Equals(level)).WriteTo.RollingFile(
                Path.Combine($"{path}\\{name}\\{DateTime.Now:yyyy-MM-dd}", name + "-{Hour}.log"), level);
        }

        #endregion



        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="level">日志等级</param>
        /// <param name="content">日志内容</param>
        public void WriteLog(LogLevel level, ILogContent content)
        {
            var provider = GetFormatProvider();
            var logEventLevel = ConvertTo(level);
            if (logEventLevel == null)
            {
                return;
            }
            if (provider == null)
            {
                throw new NullReferenceException("日志格式化提供程序不可为空");
            }
            var message = provider.Format("", content, null);
            _logger.Write(logEventLevel.SafeValue(), message);
        }

        /// <summary>
        /// 获取格式化提供chengx
        /// </summary>
        /// <returns></returns>
        private FormatProvider GetFormatProvider()
        {
            if (_format == null)
            {
                return null;
            }
            return new FormatProvider(_format);
        }

        /// <summary>
        /// 转换日志级别
        /// </summary>
        /// <param name="level">日志级别</param>
        /// <returns></returns>
        private Serilogs.Events.LogEventLevel? ConvertTo(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace:
                    return LogEventLevel.Verbose;

                case LogLevel.Debug:
                    return LogEventLevel.Debug;

                case LogLevel.Information:
                    return LogEventLevel.Information;

                case LogLevel.Warning:
                    return LogEventLevel.Warning;

                case LogLevel.Error:
                    return LogEventLevel.Error;

                case LogLevel.Fatal:
                    return LogEventLevel.Fatal;

                default:
                    return null;
            }
        }
    }
}
