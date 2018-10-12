using Bing.Helpers;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Logs.Core;
using Bing.Logs.Formats;
using Bing.Security.Extensions;
using Bing.Sessions;

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
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="providerFactory">日志提供程序工厂</param>
        /// <param name="context">日志上下文</param>
        /// <param name="format">日志格式器</param>
        /// <param name="session">用户会话</param>
        public Log(ILogProviderFactory providerFactory, ILogContext context, ILogFormat format, ISession session)
            : base(providerFactory.Create("", format), context, session)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="provider">日志提供程序</param>
        /// <param name="context">日志上下文</param>
        /// <param name="session">用户会话</param>
        /// <param name="class">类名</param>
        public Log(ILogProvider provider, ILogContext context, ISession session, string @class) : base(provider, context, session)
        {
            _class = @class;
        }

        /// <summary>
        /// 初始化一个<see cref="Log"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="provider">日志提供程序</param>
        /// <param name="context">日志上下文</param>
        /// <param name="session">用户会话</param>
        /// <param name="class">类名</param>
        public Log(string name, ILogProvider provider, ILogContext context, ISession session, string @class) : base(
            name, provider, context, session)
        {
            _class = @class;
        }

        /// <summary>
        /// 获取日志内容
        /// </summary>
        /// <returns></returns>
        protected override LogContent GetContent()
        {
            return new LogContent() { Class = _class };
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="content">日志内容</param>
        protected override void Init(LogContent content)
        {
            base.Init(content);
            content.Tenant = Session.GetTenantName();
            content.Application = Session.GetApplicationName();
            content.Operator = Session.GetFullName();
            content.Role = Session.GetRoleName();
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <returns></returns>
        public static ILog GetLog()
        {
            return GetLogByName(string.Empty);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="instance">实例</param>
        /// <returns></returns>
        public static ILog GetLog(object instance)
        {
            if (instance == null)
            {
                return GetLog();
            }
            var className = instance.GetType().ToString();
            return GetLog(className, className);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <returns></returns>
        public static ILog GetLogByName(string logName)
        {
            return GetLog(logName, string.Empty);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="class">类名</param>
        /// <returns></returns>
        private static ILog GetLog(string logName, string @class)
        {
            var providerFactory = Ioc.Create<ILogProviderFactory>();
            var format = GetLogFormat();
            var context = GetLogContext();
            var session = GetSession();
            return new Log(providerFactory.Create(logName, format), context, session, @class);
        }

        /// <summary>
        /// 获取日志格式器
        /// </summary>
        /// <returns></returns>
        private static ILogFormat GetLogFormat()
        {
            try
            {
                return Ioc.Create<ILogFormat>();
            }
            catch
            {
                return ContentFormat.Instance;
            }
        }

        /// <summary>
        /// 获取日志上下文
        /// </summary>
        /// <returns></returns>
        private static ILogContext GetLogContext()
        {
            try
            {
                return Ioc.Create<ILogContext>();
            }
            catch
            {
                return NullLogContext.Instance;
            }
        }

        /// <summary>
        /// 获取用户会话
        /// </summary>
        /// <returns></returns>
        private static ISession GetSession()
        {
            try
            {
                return Ioc.Create<ISession>();
            }
            catch
            {
                return Security.Sessions.Session.Null;
            }
        }

        /// <summary>
        /// 空日志操作
        /// </summary>
        public static readonly ILog Null = NullLog.Instance;
    }
}
