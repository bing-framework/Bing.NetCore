#nullable enable

using System;
using System.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Data.Pos.Systems;
using Bing.Admin.Data.Stores.Abstractions.Systems;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Admin.Domain.Shared;
using Bing.Admin.Modules;
using Bing.Admin.Service.FileStorage;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.AutoMapper;
using Bing.Auditing;
using Bing.Data.Enums;
using Bing.Data.Transaction;
using Bing.Dapper;
using Bing.Dapper.Sqlite;
using Bing.DependencyInjection;
using Bing.Timing;
using Bing.Users;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

#if BING_ADMIN_EFCORE_TESTS
using Bing.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Bing.Admin.EFCore.Tests;
#else
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.FreeSQL;
using FreeSql;
using FreeSql.Extensions.EfCoreFluentApi;
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>通过两套真实 SQLite 仓储验证文件权限资源初始化和部署命令。</summary>
[Collection("AdminFileHostIsolation")]
public sealed class AdminFilePermissionInitializationTests
{
    [Fact]
    public async Task DeploymentCommand_PersistsResourcesIdempotently_WithoutStartingHostOrCreatingPermissions()
    {
        await using var environment = new AdminFilePermissionInitializationTestEnvironment();
        var startupProbe = new StartupProbe();
        Guid applicationId;

        using (var seedHost = environment.BuildHost(startupProbe))
        {
            await environment.InitializeDatabaseAsync(seedHost.Services);
            applicationId = await environment.SeedAdminApplicationAsync(seedHost.Services);
        }

        var command = new[] { "files", "initialize-permissions", "--apply" };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Assert.True(await AdminFileDeployment.TryRunAsync(command,
                _ => environment.BuildHost(startupProbe)));

            using var verifyHost = environment.BuildHost(startupProbe);
            await AssertResourcesPersistedAsync(verifyHost.Services, environment, applicationId);
            Assert.False(startupProbe.Started);
        }
    }

    [Fact]
    public async Task ResourceQueries_PreserveMappedIds_ForAllAndRoleFilteredProjections()
    {
        await using var environment = new AdminFilePermissionInitializationTestEnvironment();
        var startupProbe = new StartupProbe();
        using var host = environment.BuildHost(startupProbe);
        await environment.InitializeDatabaseAsync(host.Services);
        var applicationId = await environment.SeedAdminApplicationAsync(host.Services);
        await AdminFileDeployment.TryRunAsync(new[] { "files", "initialize-permissions", "--apply" },
            _ => environment.BuildHost(startupProbe));
        using var scope = host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IResourcePoStore>();
        var modules = await store.GetModulesAsync(applicationId);
        var module = Assert.Single(modules);
        Assert.NotEqual(Guid.Empty, module.Id);
        var operations = await store.GetOperationsAsync(applicationId);
        Assert.All(operations, operation => Assert.NotEqual(Guid.Empty, operation.Id));
        Assert.Equal(module.Id, Assert.Single(await store.GetChildrenModuleAsync(applicationId, null)).Id);
        var roleId = Guid.NewGuid();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionRepository>();
        foreach (var resource in modules.Concat(operations))
            await permissions.AddAsync(new Permission(Guid.NewGuid())
            {
                RoleId = roleId, ResourceId = resource.Id, IsDeny = false
            });
        await scope.ServiceProvider.GetRequiredService<IAdminUnitOfWork>().CommitAsync();
        Assert.Equal(module.Id, Assert.Single(await store.GetModulesAsync(applicationId, new List<Guid> { roleId })).Id);
        Assert.Equal(operations.Select(x => x.Id).OrderBy(x => x),
            (await store.GetOperationsAsync(applicationId, new List<Guid> { roleId })).Select(x => x.Id).OrderBy(x => x));
        Assert.Empty(await store.GetModulesAsync(applicationId, new List<Guid> { Guid.NewGuid() }));
        Assert.Empty(await store.GetOperationsAsync(applicationId, new List<Guid> { Guid.NewGuid() }));
    }

    private static async Task AssertResourcesPersistedAsync(IServiceProvider services,
        AdminFilePermissionInitializationTestEnvironment environment, Guid applicationId)
    {
        using var scope = services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IApplicationRepository>();
        var modules = scope.ServiceProvider.GetRequiredService<IModuleRepository>();
        var operationsRepository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();

        Assert.Equal(applicationId, (await applications.GetByCodeAsync(ApplicationCode.Admin))!.Id);
        var persistedModules = await modules.GetModulesAsync(applicationId);
        var module = Assert.Single(persistedModules);
        Assert.NotEqual(Guid.Empty, module.Id);
        Assert.Equal(AdminFilePermissionCatalog.ModuleUri, module.Url);
        Assert.Equal<Guid?>(applicationId, module.ApplicationId);

        var operations = await operationsRepository.GetAllOperationsAsync(applicationId);
        var definitions = AdminFilePermissionCatalog.Operations;
        var expectedCodes = new[]
        {
            AdminFilePermissionCatalog.Upload,
            AdminFilePermissionCatalog.Publish,
            AdminFilePermissionCatalog.Manage,
            AdminFilePermissionCatalog.Host,
            AdminFilePermissionCatalog.ManageUserInfoAttachments,
            AdminFilePermissionCatalog.ManageConnections
        }.OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedCodes,
            definitions.Select(definition => definition.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray());
        Assert.Equal(expectedCodes,
            operations.Select(operation => operation.Code).OrderBy(code => code, StringComparer.Ordinal).ToArray());
        Assert.All(operations, operation =>
        {
            Assert.NotEqual(Guid.Empty, operation.Id);
            Assert.Equal(module.Id, operation.ModuleId);
        });
        Assert.Equal(0L, await environment.CountPermissionRecordsAsync(services));
    }

    private sealed class StartupProbe : IHostedService
    {
        public bool Started { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AdminFilePermissionInitializationTestEnvironment : IAsyncDisposable
    {
#if BING_ADMIN_EFCORE_TESTS
        private const string DataSourceKey = "admin-test";
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AdminUnitOfWork> _options;
#else
        private const string DataSourceKey = "admin-test";
        private readonly string _databasePath;
        private readonly SqliteConnection _connection;
        private readonly IFreeSql _orm;
        private readonly FreeSqlWrapper _wrapper;
#endif

        public AdminFilePermissionInitializationTestEnvironment()
        {
            SqlMapper.AddTypeHandler<Guid>(new SqliteGuidTypeHandler());
#if BING_ADMIN_EFCORE_TESTS
            var connectionString = $"Data Source=admin-permissions-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            _options = new DbContextOptionsBuilder<AdminUnitOfWork>().UseSqlite(_connection).Options;
#else
            _databasePath = Path.Combine(Path.GetTempPath(), $"admin-permissions-{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={_databasePath};Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            _orm = new FreeSqlBuilder()
                .UseConnectionString(DataType.Sqlite, connectionString)
                .Build();
            _wrapper = new FreeSqlWrapper { Orm = _orm };
#endif
        }

        public IHost BuildHost(StartupProbe startupProbe) => new HostBuilder()
            .ConfigureServices(services => ConfigureServices(services, startupProbe))
            .Build();

        private void ConfigureServices(IServiceCollection services, StartupProbe startupProbe)
        {
            services.AddLogging();
            services.AddAutoMapper();
            services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
            services.AddScoped<ITransactionActionManager, TransactionActionManager>();
            services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ICurrentUser>(NullCurrentUser.Instance);
            services.ConfigureSqlMetadata(options => options.DataSources.DefaultDataSourceKey = DataSourceKey);
            services.AddSqlDataSource(DataSourceKey, DatabaseType.Sqlite, _connection.ConnectionString);
            services.AddSqliteProvider();
#if BING_ADMIN_EFCORE_TESTS
            services.AddSingleton(_connection);
            services.AddSingleton(_options);
            services.AddEfCoreSqlQueryFactory();
#else
            services.Replace(ServiceDescriptor.Singleton<ISqlDbParameterCustomizer, FreeSqliteGuidParameterCustomizer>());
            services.AddSingleton<IFreeSql>(_orm);
            services.AddSingleton(_wrapper);
#endif
            services.AddScoped<AdminFilePermissionTestUnitOfWork>(provider =>
            {
#if BING_ADMIN_EFCORE_TESTS
                var unitOfWork = new AdminFilePermissionTestUnitOfWork(_options, provider)
#else
                var unitOfWork = new AdminFilePermissionTestUnitOfWork(_wrapper, provider)
#endif
                {
                    LazyServiceProvider = provider.GetRequiredService<ILazyServiceProvider>()
                };
                return unitOfWork;
            });
            services.AddScoped<IAdminUnitOfWork>(provider =>
                provider.GetRequiredService<AdminFilePermissionTestUnitOfWork>());

            services.AddScoped<IApplicationPoStore, Bing.Admin.Data.Stores.Implements.Systems.ApplicationPoStore>();
            services.AddScoped<IResourcePoStore, Bing.Admin.Data.Stores.Implements.Systems.ResourcePoStore>();
            services.AddScoped<IApplicationRepository, Bing.Admin.Data.Repositories.Systems.ApplicationRepository>();
            services.AddScoped<IModuleRepository, Bing.Admin.Data.Repositories.Systems.ModuleRepository>();
            services.AddScoped<IOperationRepository, Bing.Admin.Data.Repositories.Systems.OperationRepository>();
            services.AddScoped<IPermissionRepository, Bing.Admin.Data.Repositories.Systems.PermissionRepository>();
            services.AddBingAdminFileStoragePermissions();
            services.AddSingleton<IHostedService>(startupProbe);
        }

        public async Task InitializeDatabaseAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
#if BING_ADMIN_EFCORE_TESTS
            await scope.ServiceProvider.GetRequiredService<AdminFilePermissionTestUnitOfWork>()
                .Database.EnsureCreatedAsync();
#else
            var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminFilePermissionTestUnitOfWork>();
            _ = unitOfWork.Set<ApplicationPo>();
            _ = unitOfWork.Set<ResourcePo>();
            _ = unitOfWork.Set<Permission>();
            _orm.CodeFirst.Entity<ApplicationPo>(entity =>
            {
                entity.ToTable("AdminTestApplications");
                entity.Property(application => application.Id).HasColumnName("ApplicationId");
            });
            _orm.CodeFirst.Entity<ResourcePo>(entity =>
            {
                entity.ToTable("AdminTestResources");
                entity.Property(resource => resource.Id).HasColumnName("ResourceId");
                entity.Property(resource => resource.Path).HasColumnName("Path");
                entity.Property(resource => resource.Level).HasColumnName("Level");
            });
            _orm.CodeFirst.Entity<Permission>(entity =>
            {
                entity.ToTable("AdminTestPermissions");
                entity.Property(permission => permission.Id).HasColumnName("PermissionId");
            });
            _orm.CodeFirst.SyncStructure<ApplicationPo>();
            _orm.CodeFirst.SyncStructure<ResourcePo>();
            _orm.CodeFirst.SyncStructure<Permission>();
#endif
        }

        public async Task<Guid> SeedAdminApplicationAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var application = new Bing.Admin.Systems.Domain.Models.Application(Guid.NewGuid())
            {
                Code = ApplicationCode.Admin,
                Name = "Admin",
                Enabled = true,
                RegisterEnabled = false
            };
            application.Init();

            await scope.ServiceProvider.GetRequiredService<IApplicationRepository>().AddAsync(application);
            await scope.ServiceProvider.GetRequiredService<IAdminUnitOfWork>().CommitAsync();
            return application.Id;
        }

        public async Task<long> CountPermissionRecordsAsync(IServiceProvider services)
        {
#if BING_ADMIN_EFCORE_TESTS
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AdminFilePermissionTestUnitOfWork>()
                .Set<Permission>().CountAsync();
#else
            await Task.CompletedTask;
            return _orm.Select<Permission>().Count();
#endif
        }

        public ValueTask DisposeAsync()
        {
#if BING_ADMIN_EFCORE_TESTS
            _connection.Dispose();
#else
            _orm.Dispose();
            _connection.Dispose();
            File.Delete(_databasePath);
#endif
            return ValueTask.CompletedTask;
        }

#if !BING_ADMIN_EFCORE_TESTS
        // 两种 SQLite 驱动默认写入 GUID 的大小写不同；测试参数按 FreeSQL 的文本格式绑定。
        // 仍使用生产仓储、SQL 生成器和实际磁盘数据库，不替换任何查询或持久化结果。
        private sealed class FreeSqliteGuidParameterCustomizer : ISqlDbParameterCustomizer
        {
            private readonly SqliteDbParameterCustomizer _inner = new();
            public bool CanHandle(DatabaseType databaseType) => databaseType == DatabaseType.Sqlite;
            public void Configure(IDbDataParameter dbParameter, SqlParam sqlParameter)
            {
                _inner.Configure(dbParameter, sqlParameter);
                if (dbParameter.Value is not Guid guid) return;
                dbParameter.DbType = DbType.String;
                dbParameter.Value = guid.ToString("D");
            }
        }
#endif

        private sealed class SqliteGuidTypeHandler : SqlMapper.TypeHandler<Guid>
        {
            public override Guid Parse(object value) => value switch
            {
                Guid guid => guid,
                string text => Guid.Parse(text),
                byte[] bytes => new Guid(bytes),
                _ => Guid.Parse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!)
            };

            public override void SetValue(IDbDataParameter parameter, Guid value)
            {
                parameter.DbType = DbType.String;
                parameter.Value = value.ToString("D");
            }
        }

        private sealed class AdminFilePermissionTestUnitOfWork : AdminUnitOfWork
        {
#if BING_ADMIN_EFCORE_TESTS
            public AdminFilePermissionTestUnitOfWork(DbContextOptions<AdminUnitOfWork> options,
                IServiceProvider serviceProvider) : base(options, serviceProvider)
            {
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<ApplicationPo>(entity =>
                {
                    entity.ToTable("AdminTestApplications");
                    entity.HasKey(application => application.Id);
                    entity.Property(application => application.Id).HasColumnName("ApplicationId");
                    entity.HasQueryFilter(application => !application.IsDeleted);
                });
                modelBuilder.Entity<ResourcePo>(entity =>
                {
                    entity.ToTable("AdminTestResources");
                    entity.HasKey(resource => resource.Id);
                    entity.Property(resource => resource.Id).HasColumnName("ResourceId");
                    entity.Property(resource => resource.Path).HasColumnName("Path");
                    entity.Property(resource => resource.Level).HasColumnName("Level");
                    entity.HasQueryFilter(resource => !resource.IsDeleted);
                });
                modelBuilder.Entity<Permission>(entity =>
                {
                    entity.ToTable("AdminTestPermissions");
                    entity.HasKey(permission => permission.Id);
                    entity.Property(permission => permission.Id).HasColumnName("PermissionId");
                });
            }
#else
            public AdminFilePermissionTestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
                : base(wrapper, serviceProvider)
            {
            }

            protected override void OnModelCreating(ICodeFirst modelBuilder)
            {
                modelBuilder.Entity<ApplicationPo>(entity =>
                {
                    entity.ToTable("AdminTestApplications");
                    entity.Property(application => application.Id).HasColumnName("ApplicationId");
                });
                modelBuilder.Entity<ResourcePo>(entity =>
                {
                    entity.ToTable("AdminTestResources");
                    entity.Property(resource => resource.Id).HasColumnName("ResourceId");
                    entity.Property(resource => resource.Path).HasColumnName("Path");
                    entity.Property(resource => resource.Level).HasColumnName("Level");
                });
                modelBuilder.Entity<Permission>(entity =>
                {
                    entity.ToTable("AdminTestPermissions");
                    entity.Property(permission => permission.Id).HasColumnName("PermissionId");
                });
            }
#endif
        }
    }
}
