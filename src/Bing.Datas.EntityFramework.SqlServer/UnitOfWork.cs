using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.SqlServer
{
    /// <summary>
    /// SqlServer 工作单元
    /// </summary>
    public abstract class UnitOfWork : UnitOfWorkBase
    {
        ///// <summary>
        ///// 初始化一个<see cref="UnitOfWork"/>类型的实例
        ///// </summary>
        ///// <param name="connection">连接字符串</param>
        ///// <param name="manager">工作单元管理器</param>
        //protected UnitOfWork(string connection, IUnitOfWorkManager manager = null) : base(
        //    new DbContextOptionsBuilder().UseSqlServer(connection).Options, manager)
        //{
        //}

        ///// <summary>
        ///// 初始化一个<see cref="UnitOfWork"/>类型的实例
        ///// </summary>
        ///// <param name="connection">数据库连接</param>
        ///// <param name="manager">工作单元管理器</param>
        //protected UnitOfWork(DbConnection connection, IUnitOfWorkManager manager = null) : base(
        //    new DbContextOptionsBuilder().UseSqlServer(connection).Options, manager)
        //{
        //}

        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="manager">工作单元管理器</param>
        protected UnitOfWork(DbContextOptions options, IUnitOfWorkManager manager) : base(options, manager)
        {
        }

        /// <summary>
        /// 获取映射类型列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns></returns>
        protected override IEnumerable<Bing.Datas.EntityFramework.Core.IMap> GetMapTypes(Assembly assembly)
        {
            return Bing.Utils.Helpers.Reflection.GetInstancesByInterface<IMap>(assembly);
        }
    }
}
