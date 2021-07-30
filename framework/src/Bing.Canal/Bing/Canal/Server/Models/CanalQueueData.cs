namespace Bing.Canal.Server.Models
{
    /// <summary>
    /// Canal队列数据
    /// </summary>
    internal class CanalQueueData
    {
        /// <summary>
        /// 时间
        /// </summary>
        public string Time{ get; set; }

        /// <summary>
        /// Canal内容体
        /// </summary>
        public CanalBody CanalBody { get; set; }
    }
}
