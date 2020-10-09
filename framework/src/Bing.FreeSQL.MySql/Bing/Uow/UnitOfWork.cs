using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.FreeSQL;

namespace Bing.Uow
{
    /// <summary>
    /// MySql 工作单元
    /// </summary>
    public abstract class UnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="orm">FreeSql</param>
        /// <param name="serviceProvider">服务提供程序</param>
        protected UnitOfWork(IFreeSql orm, IServiceProvider serviceProvider = null) : base(orm, serviceProvider)
        {
        }

        /// <summary>
        /// 获取映射实例列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        protected override IEnumerable<IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<Bing.FreeSQL.MySql.IMap>(assembly);

        /// <summary>
        /// 拦截添加操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected override void InterceptAddedOperation(EntityChangeReport.ChangeInfo entry)
        {
            base.InterceptAddedOperation(entry);
            InitVersion(entry);
        }

        /// <summary>
        /// 拦截修改操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected override void InterceptModifiedOperation(EntityChangeReport.ChangeInfo entry)
        {
            base.InterceptModifiedOperation(entry);
            InitVersion(entry);
        }
    }
}
