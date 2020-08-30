using System;

namespace Bing.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    [Obsolete("请使用ICurrentUser")]
    public interface ISession
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// 是否认证
        /// </summary>
        bool IsAuthenticated { get; }
    }
}
