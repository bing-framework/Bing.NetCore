using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bing.Configurations;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Utils.Extensions;
using Exceptionless;
using el = global::Exceptionless;

namespace Bing.Logs.Exceptionless
{
    /// <summary>
    /// Exceptionless日志提供程序
    /// </summary>
    public class ExceptionlessProvider : ILogProvider
    {
        #region 属性

        /// <summary>
        /// 客户端
        /// </summary>
        private readonly el.ExceptionlessClient _client;

        /// <summary>
        /// 行号
        /// </summary>
        private int _line;

        /// <summary>
        /// 日志名称
        /// </summary>
        public string LogName { get; }

        /// <summary>
        /// 调试级别是否启用
        /// </summary>
        public bool IsDebugEnabled { get; }

        /// <summary>
        /// 跟踪级别是否启用
        /// </summary>
        public bool IsTraceEnabled { get; }

        /// <summary>
        /// 是否分布式日志
        /// </summary>
        public bool IsDistributedLog => true;

        #endregion

        /// <summary>
        /// 初始化一个<see cref="ExceptionlessProvider"/>类型的实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        public ExceptionlessProvider(string logName)
        {
            LogName = logName;
            IsDebugEnabled = BingConfig.Current.EnabledDebug;
            IsTraceEnabled = BingConfig.Current.EnabledTrace;
            _client = el.ExceptionlessClient.Default;
        }

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="level">平台日志等级</param>
        /// <param name="content">日志内容</param>
        public void WriteLog(LogLevel level, ILogContent content)
        {
            InitLine();
            var builder = CreateBuilder(level, content);
            SetUser(content);
            SetSource(builder, content);
            SetReferenceId(builder, content);
            AddProperties(builder, content as ILogConvert);
            builder.Submit();
        }

        /// <summary>
        /// 初始化行号
        /// </summary>
        private void InitLine()
        {
            _line = 1;
        }

        /// <summary>
        /// 创建事件生成器
        /// </summary>
        /// <param name="level">平台日志等级</param>
        /// <param name="content">日志内容</param>
        /// <returns></returns>
        private el.EventBuilder CreateBuilder(LogLevel level, ILogContent content)
        {
            if (content.Exception != null)
            {
                return _client.CreateException(content.Exception).AddTags(level.Description());
            }
            return _client.CreateLog(GetMessage(content), ConvertTo(level));
        }

        /// <summary>
        /// 获取日志消息
        /// </summary>
        /// <param name="content">日志内容</param>
        /// <returns></returns>
        private string GetMessage(ILogContent content)
        {
            if (content is ICaption caption && string.IsNullOrWhiteSpace(caption.Caption) == false)
            {
                return caption.Caption;
            }
            if (content.Content.Length > 0)
            {
                return content.Content.ToString();
            }
            return content.TraceId;
        }

        /// <summary>
        /// 转换日志等级
        /// </summary>
        /// <param name="level">平台日志等级</param>
        /// <returns></returns>
        private el.Logging.LogLevel ConvertTo(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Trace:
                    return el.Logging.LogLevel.Trace;
                case LogLevel.Debug:
                    return el.Logging.LogLevel.Debug;
                case LogLevel.Information:
                    return el.Logging.LogLevel.Info;
                case LogLevel.Warning:
                    return el.Logging.LogLevel.Warn;
                case LogLevel.Error:
                    return el.Logging.LogLevel.Error;
                case LogLevel.Fatal:
                    return el.Logging.LogLevel.Fatal;
                default:
                    return el.Logging.LogLevel.Off;
            }
        }

        /// <summary>
        /// 设置用户信息
        /// </summary>
        /// <param name="content"></param>
        private void SetUser(ILogContent content)
        {
            if (string.IsNullOrWhiteSpace(content.UserId))
            {
                return;
            }
            _client.Configuration.SetUserIdentity(content.UserId);
        }

        /// <summary>
        /// 设置来源
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetSource(EventBuilder builder, ILogContent content)
        {
            if (string.IsNullOrWhiteSpace(content.Url))
            {
                return;
            }
            builder.SetSource(content.Url);
        }

        /// <summary>
        /// 设置跟踪号
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetReferenceId(EventBuilder builder, ILogContent content)
        {
            builder.SetReferenceId($"{content.TraceId}");
        }

        /// <summary>
        /// 添加属性集合
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志转换器</param>
        private void AddProperties(EventBuilder builder, ILogConvert content)
        {
            if (content == null)
            {
                return;
            }
            foreach (var parameter in content.To().OrderBy(t => t.SortId))
            {
                if (string.IsNullOrWhiteSpace(parameter.Value.SafeString()))
                {
                    continue;
                }
                builder.SetProperty($"{GetLine()}. {parameter.Text}", parameter.Value);
            }
        }

        /// <summary>
        /// 获取行号
        /// </summary>
        /// <returns></returns>
        private string GetLine()
        {
            return _line++.ToString().PadLeft(2, '0');
        }
    }
}
