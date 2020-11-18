using System;
using Bing.FreeSQL;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 只读工作单元
    /// </summary>
    public class AdminReadonlyUnitOfWork : Bing.Uow.UnitOfWork, IAdminReadonlyUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminReadonlyUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="orm">FreeSql</param>
        /// <param name="serviceProvider">服务提供器</param>
        public AdminReadonlyUnitOfWork(FreeSqlWrapper orm, IServiceProvider serviceProvider = null) : base(orm, serviceProvider)
        {
        }
    }
}
