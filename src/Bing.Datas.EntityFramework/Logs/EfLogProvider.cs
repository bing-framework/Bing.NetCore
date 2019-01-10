using System;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Core;
using Bing.Logs;
using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Logs
{
    /// <summary>
    /// EF日志提供器
    /// </summary>
    public class EfLogProvider:ILoggerProvider
    {
        /// <summary>
        /// 日志操作
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// 工作单元
        /// </summary>
        private readonly UnitOfWorkBase _unitOfWork;

        /// <summary>
        /// 数据配置
        /// </summary>
        private readonly DataConfig _config;

        /// <summary>
        /// 初始化一个<see cref="EfLogProvider"/>类型的实例
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="config">数据配置</param>
        public EfLogProvider(ILog log, UnitOfWorkBase unitOfWork, DataConfig config)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _config = config;
        }

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
        /// <returns></returns>
        public ILogger CreateLogger(string categoryName)
        {
            return categoryName.StartsWith("Microsoft.EntityFrameworkCore")
                ? new EfLog(_log, _unitOfWork, categoryName, _config)
                : NullLogger.Instance;
        }
    }
}
