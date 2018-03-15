using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase:DbContext,IUnitOfWork
    {
        #region 属性

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="manager">工作单元管理器</param>
        protected UnitOfWorkBase(DbContextOptions options, IUnitOfWorkManager manager):base(options)
        {
            manager?.Register(this);
            TraceId = Guid.NewGuid().ToString();
            Session = Bing.Domains.Sessions.Session.Null;
        }

        #endregion

        public int Commit()
        {
            throw new NotImplementedException();
        }

        public Task<int> CommitAsync()
        {
            throw new NotImplementedException();
        }
    }
}
