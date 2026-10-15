using System.ComponentModel.DataAnnotations;
using AspectCore.Extensions.Hosting;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Application.Aspects;
using Bing.Auditing;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Timing;
using Bing.Uow;
using Bing.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Admin.EFCore.Tests;

/// <summary>
/// 验证 Admin EFCore 工作单元的真实提交和异常清理行为。
/// </summary>
public sealed class AdminUnitOfWorkPersistenceTest
{
    /// <summary>
    /// 验证成功调用会由工作单元管理器提交并可在新作用域读取。
    /// </summary>
    [Fact]
    public async Task CommandSuccess_ShouldPersistEntityAfterCommit()
    {
        using var connection = CreateConnection();
        using var host = CreateHost(connection);
        await EnsureDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var command = scope.ServiceProvider.GetRequiredService<IPersistenceCommand>();
            await command.CreateAsync("committed");
        }

        using var verifyScope = host.Services.CreateScope();
        var unitOfWork = verifyScope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        var record = await unitOfWork.Records.SingleAsync();
        Assert.Equal("committed", record.Value);
    }

    /// <summary>
    /// 验证目标异常不会触发工作单元提交。
    /// </summary>
    [Fact]
    public async Task CommandFailure_ShouldNotPersistEntity()
    {
        using var connection = CreateConnection();
        using var host = CreateHost(connection);
        await EnsureDatabaseAsync(host);

        using (var scope = host.Services.CreateScope())
        {
            var command = scope.ServiceProvider.GetRequiredService<IPersistenceCommand>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.CreateAndFailAsync("rolled-back"));
        }

        using var verifyScope = host.Services.CreateScope();
        var unitOfWork = verifyScope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        Assert.Empty(await unitOfWork.Records.ToListAsync());
    }

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
    /// 创建使用指定 SQLite 连接的测试宿主。
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
                services.AddScoped<IPersistenceCommand, PersistenceCommand>();
            })
            .Build();
    }

    /// <summary>
    /// 创建工作单元测试所需的数据库结构。
    /// </summary>
    /// <param name="host">测试宿主。</param>
    private static async Task EnsureDatabaseAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<TestAdminUnitOfWork>();
        await unitOfWork.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// 持久化测试命令契约。
    /// </summary>
    public interface IPersistenceCommand
    {
        /// <summary>
        /// 创建一条记录。
        /// </summary>
        Task CreateAsync(string value);

        /// <summary>
        /// 创建记录后抛出异常。
        /// </summary>
        Task CreateAndFailAsync(string value);
    }

    /// <summary>
    /// 持久化测试命令实现。
    /// </summary>
    private sealed class PersistenceCommand : IPersistenceCommand
    {
        /// <summary>
        /// 持久化测试使用的工作单元。
        /// </summary>
        private readonly TestAdminUnitOfWork _unitOfWork;
        /// <summary>
        /// 初始化一个 <see cref="PersistenceCommand" /> 类型的实例。
        /// </summary>

        public PersistenceCommand(TestAdminUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CreateAsync(string value)
        {
            _unitOfWork.Records.Add(new TestRecord { Value = value });
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CreateAndFailAsync(string value)
        {
            _unitOfWork.Records.Add(new TestRecord { Value = value });
            throw new InvalidOperationException("test failure");
        }
    }

    /// <summary>
    /// 测试用 EFCore 工作单元。
    /// </summary>
    private sealed class TestAdminUnitOfWork : AdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestAdminUnitOfWork" /> 类型的实例。
        /// </summary>
        public TestAdminUnitOfWork(
            DbContextOptions<AdminUnitOfWork> options,
            IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取测试记录集合。
        /// </summary>
        public DbSet<TestRecord> Records => Set<TestRecord>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestRecord>().HasKey(item => item.Id);
        }
    }

    /// <summary>
    /// 测试用记录实体。
    /// </summary>
    private sealed class TestRecord
    {
        /// <summary>
        /// 获取或设置主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置记录值。
        /// </summary>
        [Required]
        public string Value { get; set; }
    }
}
