namespace Bing.Logs.Abstractions
{
    /// <summary>
    /// 日志工厂
    /// </summary>
    public interface ILogFactory
    {
        /// <summary>
        /// 获取日志
        /// </summary>
        /// <param name="name">日志名称</param>
        /// <returns></returns>
        ILog GetLog(string name);
    }
}
