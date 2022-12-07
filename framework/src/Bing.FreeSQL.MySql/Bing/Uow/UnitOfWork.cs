using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.FreeSQL;

namespace Bing.Uow;

/// <summary>
/// MySql 工作单元
/// </summary>
public abstract class UnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个<see cref="UnitOfWork"/>类型的实例
    /// </summary>
    /// <param name="wrapper">FreeSql包装</param>
    /// <param name="serviceProvider">服务提供程序</param>
    protected UnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider = null) : base(wrapper, serviceProvider)
    {
    }

    /// <summary>
    /// 获取映射实例列表
    /// </summary>
    /// <param name="assembly">程序集</param>
    protected override IEnumerable<IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<Bing.FreeSQL.MySql.IMap>(assembly);
}