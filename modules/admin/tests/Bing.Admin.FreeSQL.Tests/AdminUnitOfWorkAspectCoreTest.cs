using AspectCore.Extensions.Hosting;
using Bing.Application.Aspects;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.FreeSQL;
using Bing.Uow;
using DotNetCore.CAP;
using FreeSql;
using IUnitOfWorkManager = Bing.Uow.IUnitOfWorkManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Bing.Admin.FreeSQL.Tests;

/// <summary>
/// 验证 Admin FreeSQL 工作单元在 AspectCore 容器中的属性注入行为。
/// </summary>
public sealed class AdminUnitOfWorkAspectCoreTest
{
    /// <summary>
    /// 验证继承工作单元获得当前作用域的延迟服务及事务依赖。
    /// </summary>
    [Fact]
    public async Task Resolve_ShouldInjectScopedLazyServices()
    {
        var orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
        var wrapper = new FreeSqlWrapper { Orm = orm };
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAutowired();
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddLogging();
                services.AddSingleton(wrapper);
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
            services.AddScoped<ICapPublisher>(_ => new Mock<ICapPublisher>().Object);
            services.AddScoped<AdminUnitOfWork>();
            services.AddSingleton<RecordingUnitOfWorkManager>();
            services.AddScoped<IUnitOfWorkManager>(provider =>
                provider.GetRequiredService<RecordingUnitOfWorkManager>());
            services.AddScoped<IAdminUnitOfWorkCommand, AdminUnitOfWorkCommand>();
            })
            .Build();

        await host.StartAsync();
        try
        {
            using var firstScope = host.Services.CreateScope();
            using var secondScope = host.Services.CreateScope();
            var first = firstScope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();
            var second = secondScope.ServiceProvider.GetRequiredService<AdminUnitOfWork>();

            Assert.NotNull(first.LazyServiceProvider);
            Assert.NotNull(second.LazyServiceProvider);
            Assert.Same(
                firstScope.ServiceProvider.GetRequiredService<ITransactionActionManager>(),
                first.LazyServiceProvider.LazyGetRequiredService<ITransactionActionManager>());
            Assert.Same(
                secondScope.ServiceProvider.GetRequiredService<ICapPublisher>(),
                second.LazyServiceProvider.LazyGetRequiredService<ICapPublisher>());
            Assert.NotSame(
                first.LazyServiceProvider.LazyGetRequiredService<ITransactionActionManager>(),
                second.LazyServiceProvider.LazyGetRequiredService<ITransactionActionManager>());
            Assert.Equal(0, await first.SaveChangesAsync());
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// 验证真实 Admin 工作单元依赖下的中立工作单元代理会提交一次。
    /// </summary>
    [Fact]
    public async Task MarkedCommand_ShouldCommitAfterSuccessfulCall()
    {
        using var orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
        var wrapper = new FreeSqlWrapper { Orm = orm };
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddScoped<ICapPublisher>(_ => new Mock<ICapPublisher>().Object);
                services.AddSingleton(orm);
                services.AddSingleton(wrapper);
                services.AddScoped<AdminUnitOfWork>();
                services.AddSingleton<RecordingUnitOfWorkManager>();
                services.AddScoped<IUnitOfWorkManager>(provider =>
                    provider.GetRequiredService<RecordingUnitOfWorkManager>());
                services.AddScoped<IAdminUnitOfWorkCommand, AdminUnitOfWorkCommand>();
            })
            .Build();

        var command = host.Services.GetRequiredService<IAdminUnitOfWorkCommand>();
        await command.ExecuteAsync();

        Assert.NotEqual(typeof(AdminUnitOfWorkCommand), command.GetType());
        Assert.Equal(1, host.Services.GetRequiredService<RecordingUnitOfWorkManager>().CommitCount);
        Assert.Equal(1, command.CommitAfterCount);
        Assert.Equal(new[] { "Target", "Commit", "CommitAfter" },
            host.Services.GetRequiredService<RecordingUnitOfWorkManager>().Events);
    }

    /// <summary>
    /// 验证真实 Admin 工作单元目标异常时不会提交或执行提交后回调。
    /// </summary>
    [Fact]
    public async Task MarkedCommand_ShouldNotCommitWhenTargetFails()
    {
        using var host = CreateHost();
        var command = host.Services.GetRequiredService<IAdminUnitOfWorkCommand>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAndFailAsync());

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(0, manager.CommitCount);
        Assert.Equal(0, command.CommitAfterCount);
        Assert.Equal(new[] { "Target-Failure" }, manager.Events);
    }

    /// <summary>
    /// 验证真实 Admin 工作单元嵌套调用只在最外层提交一次。
    /// </summary>
    [Fact]
    public async Task NestedCommand_ShouldCommitOnlyAtOuterBoundary()
    {
        using var host = CreateHost();
        var command = host.Services.GetRequiredService<IAdminUnitOfWorkCommand>();

        await command.ExecuteNestedAsync();

        var manager = host.Services.GetRequiredService<RecordingUnitOfWorkManager>();
        Assert.Equal(1, manager.CommitCount);
        Assert.Equal(1, command.CommitAfterCount);
        Assert.Equal(new[] { "Target-Nested", "Target-Nested-Inner", "Commit", "CommitAfter" },
            manager.Events);
    }

    /// <summary>
    /// 创建配置了 FreeSQL 工作单元及 AspectCore 拦截器的测试宿主。
    /// </summary>
    /// <returns>构建完成的测试宿主。</returns>
    private static IHost CreateHost()
    {
        using var orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
        var wrapper = new FreeSqlWrapper { Orm = orm };
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.EnableBingUnitOfWork();
                services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
                services.AddScoped<ITransactionActionManager, TransactionActionManager>();
                services.AddScoped<ICapPublisher>(_ => new Mock<ICapPublisher>().Object);
                services.AddSingleton(wrapper);
                services.AddScoped<AdminUnitOfWork>();
                services.AddSingleton<RecordingUnitOfWorkManager>();
                services.AddScoped<IUnitOfWorkManager>(provider =>
                    provider.GetRequiredService<RecordingUnitOfWorkManager>());
                services.AddScoped<IAdminUnitOfWorkCommand, AdminUnitOfWorkCommand>();
            })
            .Build();
    }

    /// <summary>
    /// 定义管理端 FreeSQL 工作单元测试命令。
    /// </summary>
    public interface IAdminUnitOfWorkCommand : ICommitAfter
    {
        /// <summary>
        /// 执行保存操作。
        /// </summary>
        Task ExecuteAsync();

        /// <summary>
        /// 执行并抛出异常的操作。
        /// </summary>
        Task ExecuteAndFailAsync();

        /// <summary>
        /// 执行嵌套工作单元操作。
        /// </summary>
        Task ExecuteNestedAsync();

        /// <summary>
        /// 执行嵌套工作单元中的内部操作。
        /// </summary>
        Task ExecuteNestedInnerAsync();

        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        int CommitAfterCount { get; }
    }

    /// <summary>
    /// 执行并记录管理端工作单元测试操作的代理目标。
    /// </summary>
    private sealed class AdminUnitOfWorkCommand : IAdminUnitOfWorkCommand
    {
        /// <summary>
        /// 执行被测操作的管理端工作单元。
        /// </summary>
        private readonly AdminUnitOfWork _unitOfWork;

        /// <summary>
        /// 用于解析测试依赖的服务提供程序。
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// 记录提交行为的工作单元管理器。
        /// </summary>
        private readonly RecordingUnitOfWorkManager _manager;

        /// <summary>
        /// 初始化一个 <see cref="AdminUnitOfWorkCommand" /> 类型的实例。
        /// </summary>
        /// <param name="unitOfWork">管理端工作单元。</param>
        /// <param name="serviceProvider">测试服务提供程序。</param>
        /// <param name="manager">工作单元事件记录器。</param>
        public AdminUnitOfWorkCommand(
            AdminUnitOfWork unitOfWork,
            IServiceProvider serviceProvider,
            RecordingUnitOfWorkManager manager)
        {
            _unitOfWork = unitOfWork;
            _serviceProvider = serviceProvider;
            _manager = manager;
        }

        /// <summary>
        /// 获取提交后回调的执行次数。
        /// </summary>
        public int CommitAfterCount { get; private set; }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteAsync()
        {
            _manager.Record("Target");
            await _unitOfWork.SaveChangesAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteAndFailAsync()
        {
            _manager.Record("Target-Failure");
            throw new InvalidOperationException("target failure");
        }

        /// <inheritdoc />
        [UnitOfWork]
        public async Task ExecuteNestedAsync()
        {
            _manager.Record("Target-Nested");
            await _serviceProvider.GetRequiredService<IAdminUnitOfWorkCommand>().ExecuteNestedInnerAsync();
        }

        /// <inheritdoc />
        [UnitOfWork]
        public Task ExecuteNestedInnerAsync()
        {
            _manager.Record("Target-Nested-Inner");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void CommitAfter()
        {
            CommitAfterCount++;
            _manager.Record("CommitAfter");
        }
    }

    /// <summary>
    /// 记录工作单元提交行为的测试管理器。
    /// </summary>
    private sealed class RecordingUnitOfWorkManager : IUnitOfWorkManager
    {
        /// <summary>
        /// 获取工作单元提交次数。
        /// </summary>
        public int CommitCount { get; private set; }

        /// <summary>
        /// 获取按顺序记录的工作单元事件。
        /// </summary>
        public List<string> Events { get; } = new();

        /// <inheritdoc />
        public void Commit()
        {
            CommitCount++;
            Record("Commit");
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            Record("Commit");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 记录一条工作单元事件。
        /// </summary>
        /// <param name="value">事件名称。</param>
        public void Record(string value) => Events.Add(value);

        /// <inheritdoc />
        public void Register(Bing.Uow.IUnitOfWork unitOfWork)
        {
        }

        /// <inheritdoc />
        public IReadOnlyCollection<Bing.Uow.IUnitOfWork> GetUnitOfWorks() => Array.Empty<Bing.Uow.IUnitOfWork>();
    }
}
