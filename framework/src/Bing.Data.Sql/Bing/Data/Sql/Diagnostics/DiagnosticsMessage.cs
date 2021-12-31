using System;
using Bing.Data.Enums;

namespace Bing.Data.Sql.Diagnostics
{
    /// <summary>
    /// 诊断消息
    /// </summary>
    public class DiagnosticsMessage
    {
        /// <summary>
        /// 操作时间戳
        /// </summary>
        public long? OperationTimestamp { get; set; }

        /// <summary>
        /// 操作
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// 操作标识
        /// </summary>
        public string OperationId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Sql语句
        /// </summary>
        public string Sql { get; set; }

        /// <summary>
        /// Sql参数
        /// </summary>
        public object Parameters { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public DatabaseType DatabaseType { get; set; }

        /// <summary>
        /// 数据库数据源
        /// </summary>
        public string DataSource { get; set; }

        /// <summary>
        /// 耗时
        /// </summary>
        public long? ElapsedMilliseconds { get; set; }

        /// <summary>
        /// 异常
        /// </summary>
        public Exception Exception { get; set; }
    }
}
