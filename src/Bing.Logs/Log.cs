using Bing.Domains.Sessions;
using Bing.Helpers;
using Bing.Logs.Abstractions;
using Bing.Logs.Contents;
using Bing.Logs.Core;
using Bing.Logs.Formats;

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
            //content.Tenant = Session.GetTenant();
            //content.Application = UserContext.GetApplication();
            //content.Operator = UserContext.GetFullName();
            //content.Role = UserContext.GetRoleName();
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="name">服务名</param>
        /// <returns></returns>
        public static ILog GetLog(string name = null)
        {
            return GetLogByName(string.Empty, name);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="instance">实例</param>
        /// <param name="name">服务名</param>
        /// <returns></returns>
        public static ILog GetLog(object instance, string name = null)
        {
            if (instance == null)
            {
                return GetLog();
            }

            var className = instance.GetType().ToString();
            return GetLog(className, className, name);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="name">服务名</param>
        /// <returns></returns>
        public static ILog GetLogByName(string logName, string name = null)
        {
            return GetLog(logName, string.Empty, name);
        }

        /// <summary>
        /// 获取日志操作实例
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="class">类名</param>
        /// <param name="name">服务名</param>
        /// <returns></returns>
        private static ILog GetLog(string logName, string @class,string name)
        {
            var providerFactory = Ioc.Create<ILogProviderFactory>(name);
            var format = GetLogFormat(name);
            var context = GetLogContext(name);
            var session = GetSession(name);
            return new Log(providerFactory.Create(logName, format), context, session, @class);
        }

        /// <summary>
        /// 获取日志格式器
        /// </summary>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        private static ILogFormat GetLogFormat(string name)
        {
            try
            {
                return Ioc.Create<ILogFormat>(name);
            }
            catch
            {
                return ContentFormat.Instance;
            }
        }

        /// <summary>
        /// 获取日志上下文
        /// </summary>
        /// <param name="name">服务名称</param>
        /// <returns></returns>
        private static ILogContext GetLogContext(string name)
        {
            try
            {
                return Ioc.Create<ILogContext>(name);
            }
            catch
            {
                return NullLogContext.Instance;
            }
        }

        /// <summary>
        /// 获取用户会话
        /// </summary>
        /// <param name="name">服务名</param>
        /// <returns></returns>
        private static ISession GetSession(string name)
        {
            try
            {
                return Ioc.Create<ISession>(name);
            }
            catch
            {
                return Bing.Domains.Sessions.Session.Null;
            }
        }

        /// <summary>
        /// 空日志操作
        /// </summary>
        public static readonly ILog Null = NullLog.Instance;
    }
}
