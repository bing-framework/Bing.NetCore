namespace Bing.Logging.Core.LogFields
{
    /// <summary>
    /// 日志字段类型
    /// </summary>
    public enum LogFieldTypes
    {
        /// <summary>
        /// 日志事件级别
        /// </summary>
        LogEventLevel,
        
        /// <summary>
        /// 消息模板
        /// </summary>
        MessageTemplate,
        
        /// <summary>
        /// 异常
        /// </summary>
        Exception,

        /// <summary>
        /// 参数
        /// </summary>
        Args,

        /// <summary>
        /// 标签
        /// </summary>
        Tags,

        /// <summary>
        /// 业务事件ID 或 业务跟踪ID
        /// </summary>
        TrackId
    }
}
