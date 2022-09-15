namespace Bing.AspNetCore.Logs
{
    /// <summary>
    /// 请求响应日志创建者
    /// </summary>
    public interface IRequestResponseLogCreator
    {
        /// <summary>
        /// 请求响应日志
        /// </summary>
        RequestResponseLog Log { get; }

        /// <summary>
        /// 输出Json字符串
        /// </summary>
        string ToJsonString();
    }
}
