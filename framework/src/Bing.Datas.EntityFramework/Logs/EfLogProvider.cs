using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Logs
{
    /// <summary>
    /// EF日志提供器
    /// </summary>
    public class EfLogProvider : ILoggerProvider
    {
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// 初始化EF日志提供器
        /// </summary>
        /// <param name="categoryName">日志分类</param>
        public ILogger CreateLogger(string categoryName) =>
            categoryName.StartsWith("Microsoft.EntityFrameworkCore")
                ? new EfLog()
                : NullLogger.Instance;
    }
}
