using Bing.Biz.FileManagement;
using Bing.FileStorage.RemoteDrive;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Biz.FileManagement.FreeSql;

/// <summary>FreeSql 文件管理仓储注册入口。</summary>
public static class FreeSqlFileManagementRegistration
{
    /// <summary>
    /// 注册 FreeSql 仓储并借用宿主提供的 ORM 客户端。
    /// </summary>
    /// <param name="services">依赖注入服务集合。</param>
    /// <param name="ormFactory">返回宿主拥有的长生命周期 ORM 客户端的工厂。</param>
    /// <returns>更新后的服务集合。</returns>
    /// <remarks>
    /// 工厂的结果缓存为单例并由本组件借用，不会由本组件释放；负责创建 ORM 的宿主必须安排其生命周期。
    /// 此注册不会创建或同步数据库表。
    /// </remarks>
    public static IServiceCollection AddBingFileManagementFreeSql(
        this IServiceCollection services,
        Func<IServiceProvider, IFreeSql> ormFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(ormFactory);

        services.TryAddSingleton(provider => new FreeSqlFileManagementOrm(ormFactory(provider)));
        services.TryAddScoped<FreeSqlFileManagementStore>(provider =>
            new FreeSqlFileManagementStore(provider.GetRequiredService<FreeSqlFileManagementOrm>().Orm));
        services.TryAddScoped<IFileManagementStore>(provider => provider.GetRequiredService<FreeSqlFileManagementStore>());
        services.TryAddScoped<IRemoteDriveStateStore>(provider => provider.GetRequiredService<FreeSqlFileManagementStore>());
        services.TryAddScoped<IFileStorageTargetStore>(provider => provider.GetRequiredService<FreeSqlFileManagementStore>());
        return services;
    }
}

internal sealed class FreeSqlFileManagementOrm
{
    internal FreeSqlFileManagementOrm(IFreeSql orm)
    {
        Orm = orm ?? throw new ArgumentNullException(nameof(orm));
        FreeSqlFileManagementModel.Configure(Orm);
    }

    internal IFreeSql Orm { get; }
}
