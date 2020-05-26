namespace Bing.Clients
{
    /// <summary>
    /// 当前客户端
    /// </summary>
    public interface ICurrentClient
    {
        /// <summary>
        /// 标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 是否已认证
        /// </summary>
        bool IsAuthenticated { get; }
    }
}
