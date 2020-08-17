using System;
using System.Collections.Generic;

namespace Bing.Monitoring.Health
{
    /// <summary>
    /// 业务健康
    /// </summary>
    public readonly struct HealthResult
    {
        /// <summary>
        /// 空只读字典
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly IReadOnlyDictionary<string, object> _emptyReadOnlyDictionary = new Dictionary<string, object>();

        /// <summary>
        /// 初始化一个<see cref="HealthResult"/>类型的实例
        /// </summary>
        /// <param name="status">业务健康状态</param>
        /// <param name="description">描述</param>
        /// <param name="exception">异常</param>
        /// <param name="data">数据</param>
        private HealthResult(BusHealthStatus status, string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null)
        {
            Status = status;
            Description = description;
            Exception = exception;
            Data = data ?? _emptyReadOnlyDictionary;
        }

        /// <summary>
        /// 数据
        /// </summary>
        public readonly IReadOnlyDictionary<string, object> Data;

        /// <summary>
        /// 描述
        /// </summary>
        public readonly string Description;

        /// <summary>
        /// 异常
        /// </summary>
        public readonly Exception Exception;

        /// <summary>
        /// 业务健康状态
        /// </summary>
        public readonly BusHealthStatus Status;

        /// <summary>
        /// 健康的
        /// </summary>
        /// <param name="description">描述</param>
        /// <param name="data">数据</param>
        public static HealthResult Healthy(string description = null, IReadOnlyDictionary<string, object> data = null) => new HealthResult(BusHealthStatus.Healthy, description, null, data);

        /// <summary>
        /// 降级的
        /// </summary>
        /// <param name="description">描述</param>
        /// <param name="exception">异常</param>
        /// <param name="data">数据</param>
        public static HealthResult Degraded(string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null) =>
            new HealthResult(BusHealthStatus.Degraded, description, exception, data);

        /// <summary>
        /// 不良的
        /// </summary>
        /// <param name="description">描述</param>
        /// <param name="exception">异常</param>
        /// <param name="data">数据</param>
        public static HealthResult Unhealthy(string description = null, Exception exception = null, IReadOnlyDictionary<string, object> data = null) =>
            new HealthResult(BusHealthStatus.Unhealthy, description, exception, data);
    }
}
