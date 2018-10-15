using Microsoft.EntityFrameworkCore;
using Bing.Datas.UnitOfWorks;

namespace Bing.DbDesigner.Data.UnitOfWorks.PgSql {
    /// <summary>
    /// 工作单元
    /// </summary>
    public class DbDesignerUnitOfWork : Bing.Datas.EntityFramework.PgSql.UnitOfWork,IDbDesignerUnitOfWork {
        /// <summary>
        /// 初始化工作单元
        /// </summary>
        /// <param name="options">配置项</param>
        /// <param name="unitOfWorkManager">工作单元服务</param>
        public DbDesignerUnitOfWork( DbContextOptions options, IUnitOfWorkManager unitOfWorkManager ) : base( options, unitOfWorkManager ) {
        }
    }
}