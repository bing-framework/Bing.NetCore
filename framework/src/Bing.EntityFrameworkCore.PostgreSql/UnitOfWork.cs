using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.PgSql;

/// <summary>
/// PgSql 工作单元
/// </summary>
public abstract class UnitOfWork : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="UnitOfWork"/> 类型的实例。
    /// </summary>
    /// <param name="options">配置</param>
    /// <param name="serviceProvider">服务提供器</param>
    protected UnitOfWork(DbContextOptions options, IServiceProvider serviceProvider = null) : base(options, serviceProvider)
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<Core.IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);
}