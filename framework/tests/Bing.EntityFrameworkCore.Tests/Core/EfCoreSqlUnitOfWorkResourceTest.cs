using Bing.Datas.EntityFramework.Core;
using Bing.EntityFrameworkCore.Tests.Core;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// 验证 EF Core Uow 资源事务和连接所有权。
/// </summary>
public sealed class EfCoreSqlUnitOfWorkResourceTest
{
    [Fact]
    public async Task EnsureReady_ShouldCreateOneTransactionAndCloseOnlyConnectionOpenedHere()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        try
        {
            using var provider = setup.Provider;
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var manager = services.GetRequiredService<IUnitOfWorkScopeManager>();
            await using var scope = manager.Begin();
            var mappedProvider = services.GetServices<IUnitOfWorkConnectionProvider>().Single();
            var resource = mappedProvider.GetResource(scope, "orders", "bing.sqlite", null,
                setup.ConnectionString);

            await resource.EnsureReadyAsync();
            var transaction = context.Database.CurrentTransaction;
            await resource.EnsureReadyAsync();

            Assert.NotNull(transaction);
            Assert.Same(transaction, context.Database.CurrentTransaction);
            Assert.Same(transaction.GetDbTransaction(), resource.Transaction);
            Assert.Equal(System.Data.ConnectionState.Open, resource.Connection.State);
            await scope.RollbackAsync();
            await scope.DisposeAsync();
            Assert.Null(context.Database.CurrentTransaction);
            Assert.Equal(System.Data.ConnectionState.Closed, resource.Connection.State);
            await resource.DisposeAsync();
        }
        finally
        {
            setup.Provider.Dispose();
            DeleteDatabase(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task Dispose_ShouldLeaveInitiallyOpenEfConnectionOpen()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        try
        {
            using var provider = setup.Provider;
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            await context.Database.OpenConnectionAsync();
            await using var scope = services.GetRequiredService<IUnitOfWorkScopeManager>().Begin();
            var resource = services.GetServices<IUnitOfWorkConnectionProvider>().Single()
                .GetResource(scope, "orders", "bing.sqlite", null, setup.ConnectionString);

            await resource.EnsureReadyAsync();
            await scope.RollbackAsync();

            Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
            await context.Database.CloseConnectionAsync();
        }
        finally
        {
            setup.Provider.Dispose();
            DeleteDatabase(setup.DatabasePath);
        }
    }

    [Fact]
    public async Task EnsureReady_ShouldRejectForeignTransactionAndRequiresNewContextReuse()
    {
        var setup = EfCoreSqliteUnitOfWorkTestSupport.CreateServices();
        try
        {
            using var provider = setup.Provider;
            await using var diScope = provider.CreateAsyncScope();
            var services = diScope.ServiceProvider;
            var context = services.GetRequiredService<EfSqliteMappedUnitOfWork>();
            var manager = services.GetRequiredService<IUnitOfWorkScopeManager>();
            var connectionProvider = services.GetServices<IUnitOfWorkConnectionProvider>().Single();
            await using var outer = manager.Begin();
            connectionProvider.GetResource(outer, "orders", "bing.sqlite", null, setup.ConnectionString);
            var inner = manager.Begin(requiresNew: true);

            var reuseException = Assert.Throws<InvalidOperationException>(() =>
                connectionProvider.GetResource(inner, "orders", "bing.sqlite", null, setup.ConnectionString));
            Assert.Contains("不同工作单元", reuseException.Message);
            await inner.RollbackAsync();
            await inner.DisposeAsync();

            await outer.RollbackAsync();
            await outer.DisposeAsync();
            await context.Database.BeginTransactionAsync();
            await using var foreignScope = manager.Begin();
            var foreignResource = connectionProvider.GetResource(foreignScope, "orders", "bing.sqlite", null,
                setup.ConnectionString);
            var foreignException = Assert.Throws<InvalidOperationException>(() => foreignResource.EnsureReady());
            Assert.Contains("已有非当前", foreignException.Message);
            await foreignScope.RollbackAsync();
            await context.Database.RollbackTransactionAsync();
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
}
