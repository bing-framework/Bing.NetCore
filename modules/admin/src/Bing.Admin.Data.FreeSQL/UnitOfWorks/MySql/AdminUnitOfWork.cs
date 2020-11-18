using System;
using Bing.FreeSQL;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Uow.UnitOfWork, IAdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="orm">FreeSql</param>
        /// <param name="serviceProvider">服务提供器</param>
        public AdminUnitOfWork(FreeSqlWrapper orm, IServiceProvider serviceProvider = null) : base(orm, serviceProvider)
        {
        }
    }
}
