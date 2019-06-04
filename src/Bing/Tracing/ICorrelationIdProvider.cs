namespace Bing.Tracing
{
    /// <summary>
    /// 跟踪关联ID提供程序
    /// </summary>
    public interface ICorrelationIdProvider
    {
        /// <summary>
        /// 获取跟踪关联ID
        /// </summary>
        string Get();
    }
}
