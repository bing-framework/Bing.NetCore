using System;
using System.Collections.Generic;
using System.Text;
using Bing.Logs;
using Bing.Sessions;

namespace Bing.Applications
{
    /// <summary>
    /// 应用服务
    /// </summary>
    public abstract class ServiceBase:IService
    {
        /// <summary>
        /// 日志
        /// </summary>
        public ILog Log { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }


        /// <summary>
        /// 初始化一个<see cref="ServiceBase"/>类型的实例
        /// </summary>
        protected ServiceBase()
        {
            Log = Bing.Logs.Log.Null;
            //Session = Bing.Domains.Sessions.Session.Null;
        }
    }
}
