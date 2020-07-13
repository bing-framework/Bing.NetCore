using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Bing.Datas.EntityFramework.Oracle
{
    /// <summary>
    /// Oracle工作单元
    /// </summary>
    public abstract class UnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="serviceProvider">服务提供器</param>
        protected UnitOfWork(DbContextOptions options, IServiceProvider serviceProvider = null) : base(options,
            serviceProvider)
        {
        }

        /// <summary>
        /// 获取映射实例列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        protected override IEnumerable<Bing.Datas.EntityFramework.Core.IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);

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
