using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Datas.EntityFramework.Core;
using Bing.EntityFrameworkCore;
using Bing.EntityFrameworkCore.Tests.Core;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// 验证 EF Core 数据源映射 provider 的注册、选择和 context 身份契约。
/// </summary>
public sealed class EfCoreSqlUnitOfWorkConnectionProviderTest
{
    [Fact]
    public void AddEfCoreSqlUnitOfWork_ShouldBeIdempotentAndRejectConflictingContext()
    {
        var services = new ServiceCollection();
        services.AddEfCoreSqlUnitOfWork<EfSqliteMappedUnitOfWork>("orders");
        services.AddEfCoreSqlUnitOfWork<EfSqliteMappedUnitOfWork>("orders");

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWorkConnectionProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType.FullName ==
            "Bing.Datas.EntityFramework.Core.EfCoreSqlUnitOfWorkMapping");
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWorkScopeManager));
        Assert.Throws<InvalidOperationException>(() =>
            services.AddEfCoreSqlUnitOfWork<OtherSqliteMappedUnitOfWork>("orders"));

        Assert.Throws<InvalidOperationException>(() =>
            services.AddEfCoreSqlUnitOfWork<EfSqliteMappedUnitOfWork>("reporting"));
    }

    [Fact]
    public void AddEfCoreSqlUnitOfWork_ShouldPreserveCustomScopeManagerRegistration()
    {
        var services = new ServiceCollection();
        var customManager = ServiceDescriptor.Scoped<IUnitOfWorkScopeManager>(_ =>
            throw new NotSupportedException("Registration precedence is asserted without resolving the custom manager."));
        services.Insert(0, customManager);

        services.AddEfCoreSqlUnitOfWork<EfSqliteMappedUnitOfWork>("orders");

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWorkScopeManager));
        Assert.Contains(customManager, services);
    }

    [Fact]
    public async Task GetResource_ShouldMapOneScopedContextAndLeaveOtherSourcesUnmapped()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        try
        {
            using var provider = setup.Provider;
            await EfCoreSqliteUnitOfWorkTestSupport.EnsureCreatedAsync(provider);
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var manager = services.GetRequiredService<IUnitOfWorkScopeManager>();
            await using var scope = manager.Begin();
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var connectionProvider = services.GetServices<IUnitOfWorkConnectionProvider>().Single();

            var unmapped = connectionProvider.GetResource(scope, "reporting", "bing.sqlite", null,
                setup.ConnectionString);
            var first = connectionProvider.GetResource(scope, "orders", "bing.sqlite", null,
                setup.ConnectionString);
            var second = connectionProvider.GetResource(scope, "orders", "bing.sqlite", null,
                setup.ConnectionString);

            Assert.Null(unmapped);
            Assert.Same(first, second);
            Assert.Same(context.Database.GetDbConnection(), first.Connection);
            Assert.Equal(System.Data.ConnectionState.Closed, first.Connection.State);
            await scope.RollbackAsync();
        }
        finally
        {
            setup.Provider.Dispose();
            DeleteDatabase(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task EnsureMappedContextMatches_ShouldRejectAnotherScopedContextInstance()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        try
        {
            using var provider = setup.Provider;
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var registered = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var options = new DbContextOptionsBuilder<EfSqliteMappedUnitOfWork>()
                .UseSqlite(setup.ConnectionString).Options;
            using var different = new EfSqliteMappedUnitOfWork(options, services);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                services.GetRequiredService<IEfCoreSqlQueryFactory>()
                    .Create(different, EfCoreSqlConnectionMode.Shared, "orders"));

            Assert.NotSame(registered, different);
            Assert.Contains("不是同一实例", exception.Message);
        }
        finally
        {
            setup.Provider.Dispose();
            DeleteDatabase(setup.DatabasePath);
        }
    }

    private static void DeleteDatabase(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed class OtherSqliteMappedUnitOfWork : EfSqliteMappedUnitOfWork
    {
        public OtherSqliteMappedUnitOfWork(DbContextOptions<EfSqliteMappedUnitOfWork> options,
            IServiceProvider serviceProvider) : base(options, serviceProvider)
        {
        }
    }
}
