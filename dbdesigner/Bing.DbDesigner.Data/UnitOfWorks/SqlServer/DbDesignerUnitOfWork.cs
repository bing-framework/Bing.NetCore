using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace Bing.DbDesigner.Data.UnitOfWorks.SqlServer
{
    /// <summary>
    /// DbDesigner 工作单元
    /// </summary>
    public class DbDesignerUnitOfWork:Bing.Datas.EntityFramework.SqlServer.UnitOfWork,IDbDesignerUnitOfWork
    {
        /// <summary>
        /// 初始化一个<see cref="DbDesignerUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="connection">连接字符串</param>
        /// <param name="manager">工作单元管理器</param>
        public DbDesignerUnitOfWork(string connection, IUnitOfWorkManager manager = null) : base(connection, manager)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="DbDesignerUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <param name="manager">工作单元管理器</param>
        public DbDesignerUnitOfWork(DbConnection connection, IUnitOfWorkManager manager = null) : base(connection, manager)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="DbDesignerUnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="manager">工作单元管理器</param>
        public DbDesignerUnitOfWork(DbContextOptions options, IUnitOfWorkManager manager) : base(options, manager)
        {

        }
    }
}
