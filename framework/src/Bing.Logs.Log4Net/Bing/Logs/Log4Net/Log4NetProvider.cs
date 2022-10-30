using System;
using System.IO;
using Bing.Logs.Abstractions;
using Bing.Logs.Formats;

namespace Bing.Logs.Log4Net;

/// <summary>
/// Log4Net 日志提供程序
/// </summary>
public class Log4NetProvider : ILogProvider
{
    #region 属性

    /// <summary>
    /// Log4Net 日志操作
    /// </summary>
    private readonly log4net.ILog _log;

    /// <summary>
    /// Log4Net 日志仓储
    /// </summary>
    internal static log4net.Repository.ILoggerRepository Repository { get; set; }

    /// <summary>
    /// 日志格式化器
    /// </summary>
    private readonly ILogFormat _format;

    /// <summary>
    /// 日志名称
    /// </summary>
    public string LogName => _log.Logger.Name;

    /// <summary>
    /// 调试级别是否启用
    /// </summary>
    public bool IsDebugEnabled => _log.IsDebugEnabled;

    /// <summary>
    /// 跟踪级别是否启用，log4net 无跟踪级别
    /// </summary>
    public bool IsTraceEnabled => false;

    /// <summary>
    /// 是否分布式日志
    /// </summary>
    public bool IsDistributedLog => false;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个<see cref="Log4NetProvider"/>类型的实例
    /// </summary>
    /// <param name="logName">日志名称</param>
    /// <param name="format">日志格式化器</param>
    public Log4NetProvider(string logName, ILogFormat format = null)
    {
        _log = GetLogger(logName);
        _format = format;
    }

    #endregion

    /// <summary>
    /// 初始化日志仓储
    /// </summary>
    /// <param name="configFileName">log4net配置文件</param>
    internal static void InitRepository(string configFileName)
    {
        if (Repository == null) 
            Repository = log4net.LogManager.CreateRepository("Log4netRepository");
        //log4net.Config.XmlConfigurator.Configure(Log4NetProvider.Repository, new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFileName)));
        log4net.Config.XmlConfigurator.Configure(Log4NetProvider.Repository, new FileInfo(configFileName));
    }

    /// <summary>
    /// 获取Log4Net日志操作
    /// </summary>
    /// <param name="logName">日志名</param>
    public static log4net.ILog GetLogger(string logName) => log4net.LogManager.GetLogger(Repository.Name, logName);

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="level">日志等级</param>
    /// <param name="content">日志内容</param>
    /// <exception cref="NullReferenceException"></exception>
    public void WriteLog(LogLevel level, ILogContent content)
    {
        var provider = GetFormatProvider();
        if (provider != null)
        {
            var message = provider.Format("", content, null);
            WriteLog(level, message);
            return;
        }
        throw new NullReferenceException("日志格式化提供程序不可为空");
    }

    /// <summary>
    /// 获取格式化提供程序
    /// </summary>
    private FormatProvider GetFormatProvider() => _format == null ? null : new FormatProvider(_format);

    /// <summary>
    /// 写日志
    /// </summary>
    /// <param name="level">日志等级</param>
    /// <param name="message">日志内容</param>
    private void WriteLog(LogLevel level, object message)
    {
        switch (level)
        {
            case LogLevel.Trace:
                return;
            case LogLevel.Debug:
                _log.Debug(message);
                return;
            case LogLevel.Information:
                _log.Info(message);
                return;
            case LogLevel.Warning:
                _log.Warn(message);
                return;
            case LogLevel.Error:
                _log.Error(message);
                return;
            case LogLevel.Fatal:
                _log.Fatal(message);
                return;
        }
    }
}