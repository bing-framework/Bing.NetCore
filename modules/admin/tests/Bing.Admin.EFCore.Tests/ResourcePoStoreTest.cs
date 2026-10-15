using Bing.Admin.Data;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Data.Stores.Implements.Systems;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Auditing;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Timing;
using Bing.Uow;
using Bing.Users;
using AspectCore.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 EFCore 资源存储器的排序号查询行为。
/// </summary>
public sealed class ResourcePoStoreTest
{
    /// <summary>
    /// 验证最大排序号只匹配相同应用和父模块。
    /// </summary>
    [Fact]
    public async Task GetMaxSortIdAsync_ShouldFilterApplicationAndParent()
    {
        using var connection = CreateConnection();
        using var host = CreateHost(connection);
        await EnsureDatabaseAsync(host);

        var applicationId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        using (var scope = host.Services.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
            unitOfWork.Resources.AddRange(
                CreateResource(applicationId, parentId, 3),
                CreateResource(applicationId, parentId, 8),
                CreateResource(applicationId, Guid.NewGuid(), 100),
                CreateResource(Guid.NewGuid(), parentId, 200));
            await unitOfWork.SaveChangesAsync();
        }

        using var queryScope = host.Services.CreateScope();
        var store = queryScope.ServiceProvider.GetRequiredService<IResourcePoStore>();
        var result = await store.GetMaxSortIdAsync(applicationId, parentId);

        Assert.Equal(8, result);
    }

    /// <summary>
    /// 验证没有匹配记录时返回空值。
    /// </summary>
    [Fact]
    public async Task GetMaxSortIdAsync_ShouldReturnNullWhenNoMatch()
    {
        using var connection = CreateConnection();
        using var host = CreateHost(connection);
        await EnsureDatabaseAsync(host);

        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IResourcePoStore>();

        var result = await store.GetMaxSortIdAsync(Guid.NewGuid(), null);

        Assert.Null(result);
    }

    /// <summary>
    /// 创建用于存储测试的资源记录。
    /// </summary>
    /// <param name="applicationId">所属应用标识。</param>
    /// <param name="parentId">父资源标识。</param>
    /// <param name="sortId">排序值。</param>
    /// <returns>资源记录。</returns>
    private static ResourcePo CreateResource(Guid applicationId, Guid? parentId, int sortId) => new()
    {
        ApplicationId = applicationId,
        ParentId = parentId,
        SortId = sortId,
        Name = $"resource-{sortId}",
        Uri = $"/resource/{sortId}"
    };

    /// <summary>
    /// 创建内存 SQLite 连接。
    /// </summary>
    /// <returns>已打开的测试连接。</returns>
    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 创建使用指定 SQLite 连接的资源存储测试宿主。
    /// </summary>
    /// <param name="connection">共享内存数据库连接。</param>
    /// <returns>构建完成的测试宿主。</returns>
    private static IHost CreateHost(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AdminUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddSingleton(connection);
                services.AddSingleton(options);
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
                services.AddSingleton(NullCurrentUser.Instance);
                services.AddSingleton<IClock, SystemClock>();
                services.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
                services.AddScoped<TestAdminUnitOfWork>();
                services.AddScoped<IAdminUnitOfWork>(provider =>
                    provider.GetRequiredService<TestAdminUnitOfWork>());
                services.AddScoped<IResourcePoStore, ResourcePoStore>();
            })
            .Build();
    }

    /// <summary>
    /// 创建资源存储测试所需的数据库结构。
    /// </summary>
    /// <param name="host">测试宿主。</param>
    private static async Task EnsureDatabaseAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        await unitOfWork.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 测试用 EFCore 工作单元。
    /// </summary>
    private sealed class TestAdminUnitOfWork : AdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestAdminUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="options">数据库配置。</param>
        /// <param name="serviceProvider">服务提供器。</param>
        public TestAdminUnitOfWork(
            DbContextOptions<AdminUnitOfWork> options,
            IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取资源集合。
        /// </summary>
        public DbSet<ResourcePo> Resources => Set<ResourcePo>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ResourcePo>().HasKey(item => item.Id);
        }
    }
}
