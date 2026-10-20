using System.Reflection;
using Bing.Datas.EntityFramework.Core;
using Microsoft.EntityFrameworkCore;

namespace Bing.Datas.EntityFramework.Oracle;

/// <summary>
/// Oracle工作单元
/// </summary>
public abstract class OracleUnitOfWorkBase : UnitOfWorkBase
{
    /// <summary>
    /// 初始化一个 <see cref="OracleUnitOfWorkBase"/> 类型的实例。
    /// </summary>
    /// <param name="options">配置</param>
    /// <param name="serviceProvider">服务提供器</param>
    protected OracleUnitOfWorkBase(DbContextOptions options, IServiceProvider serviceProvider = null) 
        : base(options, serviceProvider)
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<Bing.Datas.EntityFramework.Core.IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);
}
