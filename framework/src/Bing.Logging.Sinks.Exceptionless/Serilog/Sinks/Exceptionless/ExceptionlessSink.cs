using System;
using System.Collections.Generic;
using Bing.Logging.Sinks.Exceptionless.Internals;
using Exceptionless;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Exceptionless
{
    /// <summary>
    /// Exceptionless 接收器
    /// </summary>
    public class ExceptionlessSink : ILogEventSink, IDisposable
    {
        /// <summary>
        /// 默认标签数组
        /// </summary>
        private readonly string[] _defaultTags;

        /// <summary>
        /// 附加信息操作函数
        /// </summary>
        private readonly Func<EventBuilder, EventBuilder> _additionalOperation;

        /// <summary>
        /// 是否包含属性列表
        /// </summary>
        private readonly bool _includeProperties;

        /// <summary>
        /// Exceptionless 客户端
        /// </summary>
        private readonly ExceptionlessClient _client;

        /// <summary>
        /// 初始化一个<see cref="ExceptionlessSink"/>类型的实例
        /// </summary>
        /// <param name="apiKey">API密钥</param>
        /// <param name="serverUrl">Exceptionless服务器地址</param>
        /// <param name="defaultTags">默认标签数组</param>
        /// <param name="additionalOperation">附加信息操作函数</param>
        /// <param name="includeProperties">是否包含属性列表</param>
        public ExceptionlessSink(
            string apiKey, 
            string serverUrl = null, 
            string[] defaultTags = null, 
            Func<EventBuilder, EventBuilder> additionalOperation = null, 
            bool includeProperties = true)
        {
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));
            _client = new ExceptionlessClient(config =>
            {
                if (!string.IsNullOrEmpty(apiKey) && apiKey != "API_KEY_HERE")
                    config.ApiKey = apiKey;
                if (!string.IsNullOrEmpty(serverUrl))
                    config.ServerUrl = serverUrl;
                config.UseInMemoryStorage();
                config.UseLogger(new SelfLogLogger());
            });
            _defaultTags = defaultTags;
            _additionalOperation = additionalOperation;
            _includeProperties = includeProperties;
        }

        /// <summary>
        /// 初始化一个<see cref="ExceptionlessSink"/>类型的实例
        /// </summary>
        /// <param name="additionalOperation">附加信息操作函数</param>
        /// <param name="includeProperties">是否包含属性列表</param>
        /// <param name="client">Exceptionless客户端</param>
        public ExceptionlessSink(
            Func<EventBuilder, EventBuilder> additionalOperation = null,
            bool includeProperties = true,
            ExceptionlessClient client = null)
        {
            _additionalOperation = additionalOperation;
            _includeProperties = includeProperties;
            _client = client ?? ExceptionlessClient.Default;
            if (_client.Configuration.Resolver.HasDefaultRegistration<IExceptionlessLog, NullExceptionlessLog>())
                _client.Configuration.UseLogger(new SelfLogLogger());
        }

        /// <summary>提交.</summary>
        /// <param name="logEvent">日志事件</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null || !_client.Configuration.IsValid)
                return;
            var minLogLevel = _client.Configuration.Settings.GetMinLogLevel(logEvent.GetSource());
            if (LogLevelSwitcher.Switch(logEvent.Level) < minLogLevel)
                return;

            var builder = _client
                .CreateFromLogEvent(logEvent)
                .AddTags(_defaultTags);

            if (_includeProperties && logEvent.Properties != null)
            {
                foreach (var property in logEvent.Properties)
                {
                    switch (property.Key)
                    {
                        case Constants.SourceContextPropertyName:
                            continue;
                        case Event.KnownDataKeys.UserInfo when property.Value is StructureValue uis && string.Equals(nameof(UserInfo), uis.TypeTag):
                            var userInfo = uis.FlattenProperties() as Dictionary<string, object>;
                            if (userInfo is null)
                                continue;
                            // 忽略数据属性
                            var identity = userInfo[nameof(UserInfo.Identity)] as string;
                            var name = userInfo[nameof(UserInfo.Name)] as string;
                            if (!string.IsNullOrWhiteSpace(identity) || !string.IsNullOrWhiteSpace(name))
                                builder.SetUserIdentity(identity, name);
                            break;
                        
                        case Event.KnownDataKeys.UserDescription when property.Value is StructureValue uds && string.Equals(nameof(UserDescription), uds.TypeTag):
                            var userDescription = uds.FlattenProperties() as Dictionary<string, object>;
                            if (userDescription is null)
                                continue;
                            // 忽略数据属性
                            var emailAddress = userDescription[nameof(UserDescription.EmailAddress)] as string;
                            var description = userDescription[nameof(UserDescription.Description)] as string;
                            if (!string.IsNullOrWhiteSpace(emailAddress) || !string.IsNullOrWhiteSpace(description))
                                builder.SetUserDescription(emailAddress, description);
                            break;
                        default:
                            builder.SetProperty(property.Key, property.Value.FlattenProperties());
                            break;
                    }
                }
            }

            _additionalOperation?.Invoke(builder);
            builder.Submit();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose() => _client?.ProcessQueue();
    }
}
