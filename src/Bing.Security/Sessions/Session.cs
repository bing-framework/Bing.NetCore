using System;
using System.Collections.Generic;
using System.Text;
using Bing.Helpers;
using Bing.Security.Principals;
using Bing.Sessions;

namespace Bing.Security.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    public class Session:ISession
    {
        /// <summary>
        /// 用户标识
        /// </summary>
        public string UserId => WebIdentity.Identity.GetValue(ClaimTypes.UserId);

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName => WebIdentity.Identity.GetValue(ClaimTypes.UserName);

        /// <summary>
        /// 是否认证
        /// </summary>
        public bool IsAuthenticated => WebIdentity.Identity.IsAuthenticated;

        /// <summary>
        /// 空用户会话
        /// </summary>
        public static readonly ISession Null = NullSession.Instance;
    }
}
