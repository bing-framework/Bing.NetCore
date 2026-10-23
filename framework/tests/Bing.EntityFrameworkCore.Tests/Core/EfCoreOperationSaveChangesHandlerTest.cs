using Bing.Data.Enums;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.EntityFrameworkCore;
using Bing.Uow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.EntityFrameworkCore.Tests.Core;

/// <summary>
/// 验证 EF operation 保存适配的注册和未映射 Context 拒绝行为。
/// </summary>
public sealed class EfCoreOperationSaveChangesHandlerTest
{
    [Fact]
    public void AddEfCoreOperationSaving_ShouldBeIdempotentWithoutConstructingContexts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ContextConstructionCounter>();
        services.AddDbContext<OperationSavingTestContext>(options =>
            options.UseSqlite("Data Source=:memory:"));
        services.AddEfCoreOperationSaving();
        services.AddEfCoreOperationSaving();
        services.AddBingOperations();

        Assert.Equal(1, services.Count(descriptor =>
            descriptor.ServiceType == typeof(IUnitOfWorkSaveChangesHandler)));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IUnitOfWorkSaveChangesHandler>());

        Assert.Equal(0, provider.GetRequiredService<ContextConstructionCounter>().Count);
    }

    [Fact]
    public async Task UnmappedTrackedContext_WithoutLegacyManager_ShouldFailBeforeSql()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "bing-ef-operation", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ContextConstructionCounter>();
        services.AddDbContext<OperationSavingTestContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Pooling=False"));
        services.AddEfCoreOperationSaving();
        services.AddBingOperations();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationSavingTestContext>();
        await context.Database.EnsureCreatedAsync();
        Assert.Null(scope.ServiceProvider.GetService<IUnitOfWorkManager>());
        var operation = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>()
            .BeginOperation();
        context.Records.Add(new OperationSavingTestRecord { Name = "unmapped" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.CompleteAsync());

        Assert.Contains("没有 SQL 数据源映射", exception.Message);
        await operation.DisposeAsync();
        Assert.Equal(0, await context.Records.CountAsync());
    }

    [Fact]
    public async Task OperationSave_ShouldUseResolvedContextOnceAndLeaveUnusedMappingUnconstructed()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "bing-ef-operation", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ContextConstructionCounter>();
        services.AddSingleton<UnusedContextConstructionCounter>();
        services.ConfigureSqlMetadata(options =>
        {
            options.DataSources.DefaultDataSourceKey = "orders";
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(OperationSavingTestRecord),
                TableName = "operation_saving_records"
            });
            options.EntityMappings.Add(new EntityMappingOptions
            {
                EntityType = typeof(UnusedOperationSavingTestRecord),
                TableName = "unused_operation_saving_records"
            });
        });
        services.AddSqlCore();
        services.AddSqlDataSource("orders", DatabaseType.Sqlite, connectionString);
        services.AddSqlDataSource("unused", DatabaseType.Sqlite, connectionString);
        services.AddSqliteProvider();
        services.AddUnitOfWorkScopes();
        services.AddDbContext<OperationSavingTestContext>(options => options.UseSqlite(connectionString));
        services.AddDbContext<UnusedOperationSavingTestContext>(options => options.UseSqlite(connectionString));
        services.AddEfCoreSqlUnitOfWork<OperationSavingTestContext>("orders");
        services.AddEfCoreSqlUnitOfWork<UnusedOperationSavingTestContext>("unused");
        services.AddBingOperations();

        await using var provider = services.BuildServiceProvider();
        try
        {
            await using (var initializeScope = provider.CreateAsyncScope())
                await initializeScope.ServiceProvider.GetRequiredService<OperationSavingTestContext>()
                    .Database.EnsureCreatedAsync();
            await using var scope = provider.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>();
            var constructedBeforeOperation = provider.GetRequiredService<ContextConstructionCounter>().Count;
            var operation = manager.BeginOperation();
            var context = scope.ServiceProvider.GetRequiredService<OperationSavingTestContext>();
            Assert.Same(context, scope.ServiceProvider.GetRequiredService<OperationSavingTestContext>());
            context.Records.Add(new OperationSavingTestRecord { Name = "resolved-after-root" });

            await operation.CompleteAsync();
            await operation.DisposeAsync();

            Assert.Equal(1, await context.Records.CountAsync());
            Assert.Equal(1, context.SaveAfterCount);
            Assert.Equal(constructedBeforeOperation + 1,
                provider.GetRequiredService<ContextConstructionCounter>().Count);
            Assert.Equal(0, provider.GetRequiredService<UnusedContextConstructionCounter>().Count);
        }
        finally
        {
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private sealed class ContextConstructionCounter
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Increment() => Interlocked.Increment(ref _count);
    }

    private sealed class UnusedContextConstructionCounter
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Increment() => Interlocked.Increment(ref _count);
    }

    private sealed class OperationSavingTestContext : Bing.Datas.EntityFramework.Core.UnitOfWorkBase
    {
        public OperationSavingTestContext(DbContextOptions<OperationSavingTestContext> options,
            IServiceProvider serviceProvider) : base(options, serviceProvider)
        {
            serviceProvider.GetRequiredService<ContextConstructionCounter>().Increment();
        }

        public DbSet<OperationSavingTestRecord> Records { get; set; }

        public int SaveAfterCount { get; private set; }

        protected override void SaveChangesBefore()
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OperationSavingTestRecord>(builder =>
            {
                builder.ToTable("operation_saving_records");
                builder.HasKey(item => item.Id);
                builder.Property(item => item.Name).IsRequired();
            });
        }

        protected override Task SaveChangesAfter()
        {
            SaveAfterCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class UnusedOperationSavingTestContext : Bing.Datas.EntityFramework.Core.UnitOfWorkBase
    {
        public UnusedOperationSavingTestContext(DbContextOptions<UnusedOperationSavingTestContext> options,
            IServiceProvider serviceProvider) : base(options, serviceProvider)
        {
            serviceProvider.GetRequiredService<UnusedContextConstructionCounter>().Increment();
        }

        public DbSet<UnusedOperationSavingTestRecord> Records { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnusedOperationSavingTestRecord>(builder =>
            {
                builder.ToTable("unused_operation_saving_records");
                builder.HasKey(item => item.Id);
                builder.Property(item => item.Name).IsRequired();
            });
        }
    }

    private sealed class OperationSavingTestRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    private sealed class UnusedOperationSavingTestRecord
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
