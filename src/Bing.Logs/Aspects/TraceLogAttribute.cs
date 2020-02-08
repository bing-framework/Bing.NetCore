namespace Bing.Logs.Aspects
{
    /// <summary>
    /// 跟踪日志
    /// </summary>
    public class TraceLogAttribute : LogAttributeBase
    {
        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="log">日志操作</param>
        protected override void WriteLog(ILog log)
        {
            log.Trace();
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <returns></returns>
        protected override bool Enabled(ILog log)
        {
            return log.IsTraceEnabled;
        }
    }
}
