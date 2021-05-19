using Bing.Logs.Abstractions;

namespace Bing.Logs.NLog
{
    /// <summary>
    /// NLog日志提供程序工厂
    /// </summary>
    internal class LogProviderFactory : ILogProviderFactory
    {
        /// <summary>
        /// 创建日志提供程序
        /// </summary>
        /// <param name="logName">日志名称</param>
        /// <param name="format">日志格式化器</param>
        public ILogProvider Create(string logName, ILogFormat format = null) => new NLogProvider(logName, format);
    }
}
