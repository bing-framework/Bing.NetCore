using System.Linq;
using Bing.DependencyInjection;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Logs.Core;
using Bing.Logs.Formats;
using Bing.Users;

namespace Bing.Logs
{
    /// <summary>
    /// 日志操作
    /// </summary>
    public class Log : LogBase<LogContent>
    {
        /// <summary>
        /// 类名
        /// </summary>
        private readonly string _class;

        /// <summary>
        /// 空日志操作
        /// </summary>
        public static readonly ILog Null = NullLog.Instance;

        /// <summary>
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="providerFactory">日志提供程序工厂</param>
        /// <param name="context">日志上下文</param>
        /// <param name="format">日志格式器</param>
        /// <param name="currentUser">当前用户</param>
        public Log(ILogProviderFactory providerFactory, ILogContext context, ILogFormat format, ICurrentUser currentUser)
            : base(providerFactory.Create("", format), context, currentUser)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="provider">日志提供程序</param>
        /// <param name="context">日志上下文</param>
        /// <param name="currentUser">当前用户</param>
        /// <param name="class">类名</param>
        public Log(ILogProvider provider, ILogContext context, ICurrentUser currentUser, string @class) : base(provider, context, currentUser) => _class = @class;

        /// <summary>
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="provider">日志提供程序</param>
        /// <param name="context">日志上下文</param>
        /// <param name="currentUser">当前用户</param>
        /// <param name="class">类名</param>
        public Log(string name, ILogProvider provider, ILogContext context, ICurrentUser currentUser, string @class) : base(
            name, provider, context, currentUser) =>
            _class = @class;

        /// <summary>
        /// 获取日志内容
        /// </summary>
        protected override LogContent GetContent() => new LogContent { Class = _class };

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="content">日志内容</param>
        protected override void Init(LogContent content)
        {
            base.Init(content);
            content.Tenant = CurrentUser.GetTenantName();
            content.Application = CurrentUser.GetApplicationName();
            content.Operator = CurrentUser.GetFullName();
            content.Role = CurrentUser.GetRoleNames()?.FirstOrDefault();
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        public static ILog GetLog() => GetLog(string.Empty);

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="instance">实例</param>
        public static ILog GetLog(object instance)
        {
            if (instance == null)
                return GetLog();
            var className = instance.GetType().ToString();
            return GetLog(className, className);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        public static ILog GetLog(string logName) => GetLog(logName, string.Empty);

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="class">类名</param>
        private static ILog GetLog(string logName, string @class)
        {
            var providerFactory = GetLogProviderFactory();
            var format = GetLogFormat();
            var context = GetLogContext();
            var currentUser = GetCurrentUser();
            return new Log(providerFactory.Create(logName, format), context, currentUser, @class);
        }

        /// <summary>
        /// 获取日志提供程序工厂
        /// </summary>
        private static ILogProviderFactory GetLogProviderFactory()
        {
            try
            {
                return ServiceLocator.Instance.GetService<ILogProviderFactory>() ?? NullLogProviderFactory.Instance;
            }
            catch
            {
                return NullLogProviderFactory.Instance;
            }
        }

        /// <summary>
        /// 获取日志格式器
        /// </summary>
        private static ILogFormat GetLogFormat()
        {
            try
            {
                return ServiceLocator.Instance.GetService<ILogFormat>() ?? ContentFormat.Instance;
            }
            catch
            {
                return ContentFormat.Instance;
            }
        }

        /// <summary>
        /// 获取日志上下文
        /// </summary>
        private static ILogContext GetLogContext()
        {
            try
            {
                return ServiceLocator.Instance.GetService<ILogContext>() ?? NullLogContext.Instance;
            }
            catch
            {
                return NullLogContext.Instance;
            }
        }

        /// <summary>
        /// 获取当前用户
        /// </summary>
        private static ICurrentUser GetCurrentUser()
        {
            try
            {
                return ServiceLocator.Instance.GetService<ICurrentUser>() ?? NullCurrentUser.Instance;
            }
            catch
            {
                return NullCurrentUser.Instance;
            }
        }
    }
}
