using System;
using System.Collections.Generic;
using System.Text;
using Bing.Domains.Sessions;
using Bing.Logs;
using Bing.Logs.Core;

namespace Bing.Domains.Services
{
    /// <summary>
    /// 领域服务抽象基类
    /// </summary>
    public abstract class DomainServiceBase:IDomainService
    {
        /// <summary>
        /// 初始化一个<see cref="DomainServiceBase"/>类型的实例
        /// </summary>
        protected DomainServiceBase()
        {
            Log = NullLog.Instance;
            Session = Bing.Domains.Sessions.Session.Null;
        }

        /// <summary>
        /// 日志
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }
    }
}
