using Bing.Data.Filters;
using Bing.DependencyInjection;
using Bing.Datas.EntityFramework.Core;

namespace Bing.EntityFrameworkCore.Tests.Filters;

/// <summary>
/// 软删除全局查询过滤器集成测试。
/// </summary>
public sealed class SoftDeleteFilterIntegrationTest
{
    /// <summary>
    /// 测试 - 共享过滤状态禁用软删除过滤器后应返回已删除数据，并在作用域释放后恢复排除。
    /// </summary>
    [Fact]
    public async Task Query_WhenFilterScopeIsDisabled_ShouldIncludeDeletedRowsAndRestoreAfterDispose()
    {
        // Arrange
        await using var connection = CreateOpenConnection();
        var dataFilter = new DataFilter();
        using var provider = CreateServiceProvider(dataFilter);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        await unitOfWork.Database.EnsureCreatedAsync();
        await unitOfWork.Database.ExecuteSqlRawAsync(
            "Insert Into soft_delete_samples (Name, IsDeleted) Values ('active', 0);");
        await unitOfWork.Database.ExecuteSqlRawAsync(
            "Insert Into soft_delete_samples (Name, IsDeleted) Values ('deleted', 1);");

        // Act
        var defaultRows = await unitOfWork.Samples.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        List<SoftDeletedSample> disabledRows;
        using (dataFilter.Disable<ISoftDelete>())
            disabledRows = await unitOfWork.Samples.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var restoredRows = await unitOfWork.Samples.AsNoTracking().OrderBy(item => item.Id).ToListAsync();

        // Assert
        Assert.Collection(defaultRows, item => Assert.Equal("active", item.Name));
        Assert.Collection(disabledRows,
            item => Assert.Equal("active", item.Name),
            item => Assert.Equal("deleted", item.Name));
        Assert.Collection(restoredRows, item => Assert.Equal("active", item.Name));
    }

    /// <summary>
    /// 创建并打开 SQLite 内存连接。
    /// </summary>
    /// <returns>已打开的 SQLite 内存数据库连接。</returns>
    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// 创建包含共享过滤状态的测试服务提供程序。
    /// </summary>
    /// <param name="dataFilter">共享的数据过滤器。</param>
    /// <returns>测试服务提供程序。</returns>
    private static ServiceProvider CreateServiceProvider(IDataFilter dataFilter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dataFilter);
        services.AddSingleton<IDataFilter>(dataFilter);
        services.AddSingleton<IFilter<ISoftDelete>, SoftDeleteFilter>();
        services.AddSingleton<IFilterManager, FilterManager>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用当前服务提供程序的测试工作单元。
    /// </summary>
    /// <param name="connection">SQLite 数据库连接。</param>
    /// <param name="serviceProvider">测试服务提供程序。</param>
    /// <returns>软删除查询测试工作单元。</returns>
    private static SoftDeleteUnitOfWork CreateUnitOfWork(SqliteConnection connection, IServiceProvider serviceProvider)
    {
        var options = new DbContextOptionsBuilder<SoftDeleteUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        var unitOfWork = new SoftDeleteUnitOfWork(options, serviceProvider)
        {
            LazyServiceProvider = new LazyServiceProvider(serviceProvider)
        };
        return unitOfWork;
    }

    /// <summary>
    /// 软删除查询测试工作单元。
    /// </summary>
    private sealed class SoftDeleteUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="SoftDeleteUnitOfWork"/> 类型的实例。
        /// </summary>
        /// <param name="options">数据库上下文配置。</param>
        /// <param name="serviceProvider">服务提供程序。</param>
        public SoftDeleteUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取或设置测试实体集合。
        /// </summary>
        public DbSet<SoftDeletedSample> Samples { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SoftDeletedSample>(builder =>
            {
                builder.ToTable("soft_delete_samples");
                builder.HasKey(entity => entity.Id);
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    /// <summary>
    /// 软删除测试实体。
    /// </summary>
    private sealed class SoftDeletedSample : ISoftDelete
    {
        /// <summary>
        /// 获取或设置标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置名称。
        /// </summary>
        public string Name { get; set; }

        /// <inheritdoc />
        public bool IsDeleted { get; set; }
    }
}
