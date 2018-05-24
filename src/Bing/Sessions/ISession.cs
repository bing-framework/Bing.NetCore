namespace Bing.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// 用户名
        /// </summary>
        string UserName { get; }

        /// <summary>
        /// 租户ID
        /// </summary>
        string TenantId { get; }
    }
}
