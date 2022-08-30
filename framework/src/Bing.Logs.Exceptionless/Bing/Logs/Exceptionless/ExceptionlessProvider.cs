using System.Diagnostics;
using System.Linq;
using Bing.Configuration;
using Bing.Extensions;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Logs.Exceptionless.Internal;
using Exceptionless;
using el = global::Exceptionless;

namespace Bing.Logs.Exceptionless
{
    /// <summary>
    /// Exceptionless日志提供程序
    /// </summary>
    internal class ExceptionlessProvider : ILogProvider
    {
        #region 属性

        /// <summary>
        /// 客户端
        /// </summary>
        private readonly el.ExceptionlessClient _client;

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

        /// <summary>
        /// Url过滤
        /// </summary>
        public static string[] UrlFilter { get; } = new[] { "t", "page", "pageSize" };

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
            var builder = CreateBuilder(level, content);
            // 致命错误
            if (level == LogLevel.Fatal || level == LogLevel.Error)
                builder.MarkAsCritical();
            Debug.WriteLine($"【Exceptionless】Thread: {content.ThreadId}, LogId: {content.LogId}, TraceId: {content.TraceId}, Message: {GetMessage(content)}, Tags: [{content.Tags.ExpandAndToString()}]");
            SetUser(content);
            SetSource(builder, content);
            SetReferenceId(builder, content);
            SetException(builder, content);
            AddProperties(builder, content as ILogConvert);
            AddExtraProperties(builder, content);
            AddTags(builder, content);
            
            builder.Submit();
        }

        /// <summary>
        /// 创建事件生成器
        /// </summary>
        /// <param name="level">平台日志等级</param>
        /// <param name="content">日志内容</param>
        private el.EventBuilder CreateBuilder(LogLevel level, ILogContent content)
        {
            if (content.Exception != null && (level == LogLevel.Error || level == LogLevel.Fatal))
                return _client.CreateException(content.Exception);
            var builder = _client.CreateLog(GetMessage(content), LogLevelSwitcher.Switch(level));
            if (content.Exception != null && level == LogLevel.Warning)
                builder.SetException(content.Exception);
            return builder;
        }

        /// <summary>
        /// 获取日志消息
        /// </summary>
        /// <param name="content">日志内容</param>
        private string GetMessage(ILogContent content)
        {
            if (content is ICaption caption && string.IsNullOrWhiteSpace(caption.Caption) == false)
                return caption.Caption;
            if (content.Content.Length > 0)
                return content.Content.ToString();
            return content.LogId;
        }

        /// <summary>
        /// 设置用户信息
        /// </summary>
        /// <param name="content">日志内容</param>
        private void SetUser(ILogContent content)
        {
            if (string.IsNullOrWhiteSpace(content.UserId))
                return;
            _client.Configuration.SetUserIdentity(content.UserId);
        }

        /// <summary>
        /// 设置来源
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetSource(EventBuilder builder, ILogContent content)
        {
            // 优先设置日志名称
            if (!string.IsNullOrWhiteSpace(content.LogName))
            {
                builder.SetSource(content.LogName);
                return;
            }
            if (string.IsNullOrWhiteSpace(content.Url))
                return;
            builder.SetSource(RemoveParams(content.Url, UrlFilter));
        }

        /// <summary>
        /// 移除URL参数
        /// </summary>
        /// <param name="url">Url地址</param>
        /// <param name="paramList">参数列表</param>
        private static string RemoveParams(string url, string[] paramList)
        {
            foreach (var param in paramList)
            {
                var reg = $"(?<=[\\?&]){param}=[^&]*&?";
                url = url.ReplaceWith(reg, "");
            }
            url = url.ReplaceWith("&+$", "");
            return url.TrimEnd('?');
        }

        /// <summary>
        /// 设置跟踪号
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetReferenceId(EventBuilder builder, ILogContent content)
        {
            if (!IsValidIdentifier(content.LogId))
            {
                builder.SetProperty("ReferenceIdException", content.LogId);
                return;
            }
            builder.SetReferenceId($"{content.LogId}");
        }

        /// <summary>
        /// 校验标识
        /// </summary>
        /// <param name="value">值</param>
        private static bool IsValidIdentifier(string value)
        {
            if (value == null)
                return true;

            if (value.Length < 8 || value.Length > 100)
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                if (!char.IsLetterOrDigit(value[index]) && value[index] != '-')
                    return false;
            }
            return true;
        }


        /// <summary>
        /// 设置异常
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetException(EventBuilder builder, ILogContent content)
        {
            if (content.Exception == null)
                return;
            if (content is ICaption caption && !string.IsNullOrWhiteSpace(caption.Caption))
                builder.SetMessage($"{builder.Target.Message} {content.Exception.Message}【{caption.Caption}】");
            SetExceptionData(builder, content);
        }

        /// <summary>
        /// 设置异常数据
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void SetExceptionData(EventBuilder builder, ILogContent content)
        {
            if (content?.Exception?.Data != null && content.Exception.Data.Count == 0)
                return;
            builder.SetProperty("异常数据", content?.Exception?.Data);
        }

        /// <summary>
        /// 添加属性集合
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志转换器</param>
        private void AddProperties(EventBuilder builder, ILogConvert content)
        {
            if (content == null)
                return;
            foreach (var parameter in content.To().OrderBy(t => t.SortId))
            {
                if (string.IsNullOrWhiteSpace(parameter.Value.SafeString()))
                    continue;
                builder.SetProperty(parameter.Text, parameter.Value);
            }
        }

        /// <summary>
        /// 添加扩展属性集合
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志转换器</param>
        private void AddExtraProperties(EventBuilder builder, ILogContent content)
        {
            if (content == null)
                return;
            foreach (var parameter in content.ExtraProperties)
            {
                if (string.IsNullOrWhiteSpace(parameter.Value.SafeString()))
                    continue;
                builder.SetProperty(parameter.Key, parameter.Value);
            }
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        /// <param name="builder">事件生成器</param>
        /// <param name="content">日志内容</param>
        private void AddTags(EventBuilder builder, ILogContent content)
        {
            builder.AddTags(content.Level, content.LogName, content.TraceId);
            if (content.Tags.Any())
                builder.AddTags(content.Tags.ToArray());
        }
    }
}
