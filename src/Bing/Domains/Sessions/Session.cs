using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Domains.Sessions
{
    /// <summary>
    /// 用户会话
    /// </summary>
    public class Session:ISession
    {
        /// <summary>
        /// 空用户会话
        /// </summary>
        public static readonly ISession Null=new NullSession();

        /// <summary>
        /// 用户编号
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; }

        /// <summary>
        /// 租户ID
        /// </summary>
        public string TenantId { get; }

        public Session()
        {

        }
    }
}
