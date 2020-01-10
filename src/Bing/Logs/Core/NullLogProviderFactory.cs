using Bing.Logs.Abstractions;

namespace Bing.Logs.Core
{
    /// <summary>
    /// 空日志提供程序工厂
    /// </summary>
    public class NullLogProviderFactory : ILogProviderFactory
    {
        /// <summary>
        /// 空日志提供程序工厂实例
        /// </summary>
        public static readonly ILogProviderFactory Instance = new NullLogProviderFactory();

        /// <summary>
        /// 创建日志提供程序
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="format">日志格式化器</param>
        public ILogProvider Create(string logName, ILogFormat format = null) => NullLogProvider.Instance;
    }
}
