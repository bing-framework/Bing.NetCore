namespace Bing.Sessions
{
    /// <summary>
    /// 空用户会话
    /// </summary>
    public class NullSession:ISession
    {
        /// <summary>
        /// 用户编号
        /// </summary>
        public string UserId =>string.Empty;

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName => string.Empty;

        /// <summary>
        /// 租户ID
        /// </summary>
        public string TenantId => string.Empty;

        /// <summary>
        /// 空用户会话实例
        /// </summary>
        public static readonly ISession Instance=new NullSession();
    }
}
