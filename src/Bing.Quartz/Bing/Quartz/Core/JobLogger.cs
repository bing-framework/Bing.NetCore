using System;
using System.Threading.Tasks;
using Bing.Quartz.Abstractions;
using Microsoft.Extensions.Logging;

namespace Bing.Quartz.Core
{
    /// <summary>
    /// 任务日志记录器
    /// </summary>
    public class JobLogger : IJobLogger
    {
        /// <summary>
        /// 日志记录器
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// 初始化一个<see cref="JobLogger"/>类型的实例
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public JobLogger(ILogger<JobLogger> logger) => _logger = logger;

        /// <summary>
        /// 任务标识
        /// </summary>
        public Guid JobId { get; set; }

        /// <summary>
        /// 信息
        /// </summary>
        /// <param name="msg">消息</param>
        public Task Info(string msg)
        {
            _logger.LogInformation($"任务标识:{JobId}, {msg}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 调试
        /// </summary>
        /// <param name="msg">消息</param>
        public Task Debug(string msg)
        {
            _logger.LogDebug($"任务标识:{JobId}, {msg}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异常
        /// </summary>
        /// <param name="msg">消息</param>
        public Task Error(string msg)
        {
            _logger.LogWarning($"任务标识:{JobId}, {msg}");
            return Task.CompletedTask;
        }
    }
}
