namespace Bing.Scheduler.Core
{
    /// <summary>
    /// 请求类型
    /// </summary>
    public enum RequestType
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// Get
        /// </summary>
        Get = 1,
        /// <summary>
        /// Post
        /// </summary>
        Post = 2,
        /// <summary>
        /// Put
        /// </summary>
        Put = 4,
        /// <summary>
        /// Delete
        /// </summary>
        Delete = 8
    }
}
