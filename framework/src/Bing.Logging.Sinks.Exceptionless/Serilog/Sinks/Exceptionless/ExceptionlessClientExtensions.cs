using System.Collections.Generic;
using Bing.Logging.Sinks.Exceptionless.Internals;
using Exceptionless;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Exceptionless
{
    /// <summary>
    /// Exceptionless客户端(<see cref="ExceptionlessClient"/>) 扩展
    /// </summary>
    public static class ExceptionlessClientExtensions
    {
        /// <summary>
        /// 从Serilog日志事件创建事件构建器
        /// </summary>
        /// <param name="client">Exceptionless客户端</param>
        /// <param name="log">Serilog日志事件</param>
        public static EventBuilder CreateFromLogEvent(this ExceptionlessClient client, LogEvent log)
        {
            var message = log.RenderMessage();
            
            var builder = log.Exception != null
                ? client.CreateException(log.Exception)
                : client.CreateLog(log.GetSource(), message, LogLevelSwitcher.Switch(log.Level));

            builder.Target.Date = log.Timestamp;
            if (log.Level == LogEventLevel.Fatal)
                builder.MarkAsCritical();

            if (!string.IsNullOrWhiteSpace(message))
                builder.SetMessage(message);

            return builder;
        }

        /// <summary>
        /// 从Serilog日志事件提交
        /// </summary>
        /// <param name="client">Exceptionless客户端</param>
        /// <param name="log">Serilog日志事件</param>
        public static void SubmitFromLogEvent(this ExceptionlessClient client, LogEvent log) => CreateFromLogEvent(client,log).Submit();

        /// <summary>
        /// 获取来源
        /// </summary>
        /// <param name="log">日志事件</param>
        internal static string GetSource(this LogEvent log)
        {
            if (log.Properties.TryGetValue(Constants.SourceContextPropertyName, out var value))
                return value.FlattenProperties()?.ToString();
            return null;
        }

        /// <summary>
        /// 展开属性
        /// <para>
        /// 删除了<see cref="LogEventPropertyValue"/>实现的结构化。
        /// </para>
        /// </summary>
        /// <param name="value">日志事件属性值</param>
        internal static object FlattenProperties(this LogEventPropertyValue value)
        {
            if (value == null)
                return null;

            if (value is ScalarValue scalar)
                return scalar.Value;

            if (value is SequenceValue sequence)
            {
                var flattenedProperties = new List<object>(sequence.Elements.Count);
                foreach (var element in sequence.Elements) 
                    flattenedProperties.Add(element.FlattenProperties());
                return flattenedProperties;
            }

            if (value is StructureValue structure)
            {
                var flattenedProperties = new Dictionary<string, object>(structure.Properties.Count);
                foreach (var property in structure.Properties)
                    flattenedProperties.Add(property.Name, property.Value.FlattenProperties());
                return flattenedProperties;
            }

            if (value is DictionaryValue dictionary)
            {
                var flattenedProperties = new Dictionary<object, object>(dictionary.Elements.Count);
                foreach (var element in dictionary.Elements)
                    flattenedProperties.Add(element.Key.Value, element.Value.FlattenProperties());
                return flattenedProperties;
            }

            return value;
        }
    }
}
