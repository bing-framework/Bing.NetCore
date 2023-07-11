using Bing.Datas.EntityFramework.MySql;
using Microsoft.EntityFrameworkCore;

namespace Bing.Tests.UnitOfWorks;

/// <summary>
/// MySql工作单元
/// </summary>
public class MySqlUnitOfWork : MySqlUnitOfWorkBase, ITestUnitOfWork
{
    /// <summary>
    /// 初始化一个<see cref="MySqlUnitOfWork"/>类型的实例
    /// </summary>
    /// <param name="options">配置</param>
    /// <param name="serviceProvider">服务提供器</param>
    public MySqlUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider = null) 
        : base(options, serviceProvider)
    {
    }
}
