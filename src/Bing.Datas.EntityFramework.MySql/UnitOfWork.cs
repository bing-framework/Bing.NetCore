using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bing.Datas.EntityFramework.MySql
{
    /// <summary>
    /// MySql 工作单元
    /// </summary>
    public abstract class UnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="connection">连接字符串</param>
        /// <param name="manager">工作单元管理器</param>
        protected UnitOfWork(string connection, IUnitOfWorkManager manager = null) : base(
            new DbContextOptionsBuilder().UseMySql(connection).Options, manager)
        {
        }

        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="connection">连接</param>
        /// <param name="manager">工作单元管理器</param>
        protected UnitOfWork(DbConnection connection, IUnitOfWorkManager manager = null) : base(
            new DbContextOptionsBuilder().UseMySql(connection).Options, manager)
        {
        }

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
            return Bing.Utils.Helpers.Reflection.GetTypesByInterface<IMap>(assembly);
        }

        /// <summary>
        /// 拦截添加操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected override void InterceptAddedOperation(EntityEntry entry)
        {
            base.InterceptAddedOperation(entry);
            InitVersion(entry);
        }

        /// <summary>
        /// 拦截修改操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected override void InterceptModifiedOperation(EntityEntry entry)
        {
            base.InterceptModifiedOperation(entry);
            InitVersion(entry);
        }
    }
}
