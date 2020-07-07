using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Sqlite
{
    /// <summary>
    /// Sqlite 工作单元
    /// </summary>
    public abstract class UnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个<see cref="UnitOfWork"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="serviceProvider">服务提供器</param>
        protected UnitOfWork(DbContextOptions options, IServiceProvider serviceProvider = null) : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取映射实例列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns></returns>
        protected override IEnumerable<Core.IMap> GetMapInstances(Assembly assembly)
        {
            return Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);
        }
    }
}
