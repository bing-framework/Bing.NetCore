using System;
using Microsoft.EntityFrameworkCore;

namespace Bing.Admin.Data.UnitOfWorks.MySql
{
    /// <summary>
    /// 只读工作单元
    /// </summary>
    public class AdminReadonlyUnitOfWork : Bing.Datas.EntityFramework.MySql.UnitOfWork, IAdminReadonlyUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminReadonlyUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="serviceProvider">服务提供器</param>
        public AdminReadonlyUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider = null) : base(options, serviceProvider)
        {
        }
    }
}
