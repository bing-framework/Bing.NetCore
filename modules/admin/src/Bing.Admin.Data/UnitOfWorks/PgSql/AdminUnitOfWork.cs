using Microsoft.EntityFrameworkCore;

namespace Bing.Admin.Data.UnitOfWorks.PgSql
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public class AdminUnitOfWork : Bing.Datas.EntityFramework.PgSql.UnitOfWork, IAdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="AdminUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置项</param>
        public AdminUnitOfWork( DbContextOptions<AdminUnitOfWork> options ) : base( options ) 
        {
        }
    }
}