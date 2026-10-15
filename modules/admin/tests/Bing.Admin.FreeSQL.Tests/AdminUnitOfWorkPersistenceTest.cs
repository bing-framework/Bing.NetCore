using AspectCore.Extensions.Hosting;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Application.Aspects;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.FreeSQL;
using Bing.Uow;
using FreeSql;
using IUnitOfWorkManager = Bing.Uow.IUnitOfWorkManager;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 Admin FreeSQL 工作单元的真实提交和异常清理行为。
/// </summary>
public sealed class AdminUnitOfWorkPersistenceTest
{
    /// <summary>
    /// 验证成功调用会由工作单元管理器提交并可在新作用域读取。
    /// </summary>
    [Fact]
    public async Task CommandSuccess_ShouldPersistEntityAfterCommit()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<TestRecord>();
        using var host = CreateHost(orm);

        using (var scope = host.Services.CreateScope())
        {
            var command = scope.ServiceProvider.GetRequiredService<IPersistenceCommand>();
            await command.CreateAsync("committed");
        }

        Assert.Equal(1, orm.Select<TestRecord>().Count());
        Assert.Equal("committed", orm.Select<TestRecord>().First().Value);
    }

    /// <summary>
    /// 验证目标异常不会触发工作单元提交。
    /// </summary>
    [Fact]
    public async Task CommandFailure_ShouldNotPersistEntity()
    {
        using var orm = CreateOrm();
        orm.CodeFirst.SyncStructure<TestRecord>();
        using var host = CreateHost(orm);

        using (var scope = host.Services.CreateScope())
        {
            var command = scope.ServiceProvider.GetRequiredService<IPersistenceCommand>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => command.CreateAndFailAsync("not-committed"));
        }

        Assert.Equal(0, orm.Select<TestRecord>().Count());
    }

    /// <summary>
    /// 创建使用内存 SQLite 数据库的 FreeSQL 实例。
    /// </summary>
    /// <returns>FreeSQL 实例。</returns>
    private static IFreeSql CreateOrm()
    {
        return new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
    }

    /// <summary>
    /// 创建使用指定 FreeSQL 实例的测试宿主。
    /// </summary>
    /// <param name="orm">内存数据库 ORM。</param>
    /// <returns>构建完成的测试宿主。</returns>
    private static IHost CreateHost(IFreeSql orm)
    {
        var wrapper = new FreeSqlWrapper { Orm = orm };
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddSingleton(wrapper);
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddScoped<IUnitOfWorkManager, Bing.Uow.UnitOfWorkManager>();
                services.AddScoped<TestAdminUnitOfWork>();
                services.AddScoped<IPersistenceCommand, PersistenceCommand>();
            })
            .Build();
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
            _unitOfWork.Add(new TestRecord { Value = value });
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task CreateAndFailAsync(string value)
        {
            _unitOfWork.Add(new TestRecord { Value = value });
            throw new InvalidOperationException("test failure");
        }
    }

    /// <summary>
    /// 测试用 FreeSQL 工作单元。
    /// </summary>
    private sealed class TestAdminUnitOfWork : AdminUnitOfWork
    {
        /// <summary>
        /// 初始化一个 <see cref="TestAdminUnitOfWork" /> 类型的实例。
        /// </summary>
        public TestAdminUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }
    }

    /// <summary>
    /// 测试用记录实体。
    /// </summary>
    [Table(Name = "bing_uow_aspectcore_test_record")]
    private sealed class TestRecord
    {
        /// <summary>
        /// 获取或设置主键。
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置记录值。
        /// </summary>
        public string Value { get; set; }
    }
}
