namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 结果
    /// </summary>
    public class Result
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public dynamic Data { get; set; }
    }
}
