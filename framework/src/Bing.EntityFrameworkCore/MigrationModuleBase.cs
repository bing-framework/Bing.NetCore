using Bing.Core.Modularity;
using Bing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.EntityFramework;

/// <summary>
/// 数据迁移模块基类
/// </summary>
/// <typeparam name="TDbContext">数据上下文类型</typeparam>
public abstract class MigrationModuleBase<TDbContext> : BingModule
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public override ModuleLevel Level => ModuleLevel.Framework;

    /// <inheritdoc />
    public override void UseModule(IServiceProvider provider)
    {
        var seedDataInitializers = provider.GetServices<ISeedDataInitializer>().OrderBy(m => m.Order);
        foreach (var initializer in seedDataInitializers)
            initializer.InitializeAsync().GetAwaiter().GetResult();
        Enabled = true;
    }

    /// <summary>
    /// 重写实现获取数据上下文实例
    /// </summary>
    /// <param name="scopedProvider">服务提供者</param>
    /// <returns>由服务提供程序创建的数据上下文实例。</returns>
    protected abstract TDbContext CreateDbContext(IServiceProvider scopedProvider);
}
