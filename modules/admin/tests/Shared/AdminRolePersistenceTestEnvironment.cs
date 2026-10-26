#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Systems.Domain.Models;
using Bing.Admin.Systems.Domain.Repositories;
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.DependencyInjection;
using Bing.Data.Transaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#if BING_ADMIN_EFCORE_TESTS
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Auditing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Bing.Timing;
using Bing.Users;
namespace Bing.Admin.EFCore.Tests;
#else
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.Dapper;
using Bing.FreeSQL;
using FreeSql;
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>为权限 HTTP 测试提供真实 SQLite 角色仓储。</summary>
internal sealed class AdminRolePersistenceTestEnvironment : IAsyncDisposable
{
#if BING_ADMIN_EFCORE_TESTS
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AdminUnitOfWork> _options;
#else
    private readonly IFreeSql _orm;
#endif

    public AdminRolePersistenceTestEnvironment()
    {
#if BING_ADMIN_EFCORE_TESTS
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AdminUnitOfWork>().UseSqlite(_connection).Options;
#else
        _orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
#endif
    }

    public void Configure(IServiceCollection services)
    {
        services.RemoveAll<IRoleRepository>();
        services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
#if BING_ADMIN_EFCORE_TESTS
        services.AddSingleton(_connection);
        services.AddSingleton(_options);
        services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AdminRoleTestUnitOfWork>(provider =>
        {
            var unitOfWork = new AdminRoleTestUnitOfWork(_options, provider)
            {
                LazyServiceProvider = provider.GetRequiredService<ILazyServiceProvider>()
            };
            return unitOfWork;
        });
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
#else
        services.AddSqlCore();
        services.AddSingleton(_orm);
        services.AddSingleton(new FreeSqlWrapper { Orm = _orm });
        services.AddScoped<AdminRoleTestUnitOfWork>(provider =>
        {
            var unitOfWork = new AdminRoleTestUnitOfWork(provider.GetRequiredService<FreeSqlWrapper>(), provider)
            {
                LazyServiceProvider = provider.GetRequiredService<ILazyServiceProvider>()
            };
            return unitOfWork;
        });
#endif
#if BING_ADMIN_EFCORE_TESTS
        services.AddScoped<IRoleRepository>(provider =>
            new Bing.Admin.Data.Repositories.Systems.RoleRepository(provider.GetRequiredService<AdminRoleTestUnitOfWork>()));
#else
        services.AddScoped<IRoleRepository>(provider =>
            new Bing.Admin.Data.Repositories.Systems.RoleRepository(
                provider.GetRequiredService<AdminRoleTestUnitOfWork>(),
                provider.GetRequiredService<ISqlQueryFactory>(),
                provider.GetRequiredService<IDatabaseContextAccessor>(),
                provider.GetRequiredService<SqlMetadataOptions>(),
                provider.GetRequiredService<ITypeConverterResolver>()));
#endif
    }

    public async Task<int> DisableRolesForUserAsync(IServiceProvider services, Guid userId)
    {
#if BING_ADMIN_EFCORE_TESTS
        using var scope = services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminRoleTestUnitOfWork>();
        var roleIds = await unitOfWork.Set<UserRole>().Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.RoleId).ToListAsync();
        var roles = await unitOfWork.Set<Role>().Where(role => roleIds.Contains(role.Id)).ToListAsync();
        foreach (var role in roles)
            role.Enabled = false;
        return await unitOfWork.SaveChangesAsync();
#else
        var roleIds = _orm.Select<UserRole>().Where(userRole => userRole.UserId == userId)
            .ToList(userRole => userRole.RoleId);
        return roleIds.Count == 0
            ? 0
            : _orm.Update<Role>().Set(role => role.Enabled, false).Where(role => roleIds.Contains(role.Id))
                .ExecuteAffrows();
#endif
    }

    public Task InitializeDatabaseAsync(IServiceProvider services, AdminFilePermissionTestCatalog catalog)
    {
#if BING_ADMIN_EFCORE_TESTS
        return InitializeEfDatabaseAsync(services, catalog);
#else
        using var scope = services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminRoleTestUnitOfWork>();
        _ = unitOfWork.Set<Role>();
        _ = unitOfWork.Set<UserRole>();
        var assignments = catalog.RoleAssignments.ToList();
        var disabledRoles = assignments.Where(item => !item.Role.Enabled).Select(item => item.Role).ToList();
        foreach (var assignment in assignments) assignment.Role.Enabled = true;
        _orm.CodeFirst.SyncStructure<Role>();
        _orm.CodeFirst.SyncStructure<UserRole>();
        _orm.Insert(assignments.Select(item => item.Role)).ExecuteAffrows();
        _orm.Insert(assignments.Select(item => new UserRole(item.UserId, item.Role.Id))).ExecuteAffrows();
        foreach (var role in disabledRoles) role.Enabled = false;
        if (disabledRoles.Count > 0)
            _orm.Update<Role>().SetSource(disabledRoles).ExecuteAffrows();
        return Task.CompletedTask;
#endif
    }

#if BING_ADMIN_EFCORE_TESTS
    private static async Task InitializeEfDatabaseAsync(IServiceProvider services, AdminFilePermissionTestCatalog catalog)
    {
        using var scope = services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<AdminRoleTestUnitOfWork>();
        await unitOfWork.Database.EnsureCreatedAsync();
        var assignments = catalog.RoleAssignments.ToList();
        var disabledRoles = assignments.Where(item => !item.Role.Enabled).Select(item => item.Role).ToList();
        foreach (var assignment in assignments) assignment.Role.Enabled = true;
        await unitOfWork.Set<Role>().AddRangeAsync(assignments.Select(item => item.Role));
        await unitOfWork.Set<UserRole>().AddRangeAsync(assignments.Select(item =>
            new UserRole(item.UserId, item.Role.Id)));
        await unitOfWork.SaveChangesAsync();
        foreach (var role in disabledRoles) role.Enabled = false;
        await unitOfWork.SaveChangesAsync();
    }
#endif

    public ValueTask DisposeAsync()
    {
#if BING_ADMIN_EFCORE_TESTS
        _connection.Dispose();
#else
        _orm.Dispose();
#endif
        return ValueTask.CompletedTask;
    }

#if BING_ADMIN_EFCORE_TESTS
    private sealed class AdminRoleTestUnitOfWork : AdminUnitOfWork
    {
        public AdminRoleTestUnitOfWork(DbContextOptions<AdminUnitOfWork> options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("AdminTestRoles");
                entity.HasKey(role => role.Id);
                entity.HasQueryFilter(role => !role.IsDeleted);
            });
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.ToTable("AdminTestUserRoles");
                entity.HasKey(userRole => new { userRole.UserId, userRole.RoleId });
            });
        }
    }
#else
    private sealed class AdminRoleTestUnitOfWork : AdminUnitOfWork
    {
        public AdminRoleTestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }

        protected override void OnModelCreating(ICodeFirst modelBuilder)
        {
            modelBuilder.ConfigEntity<Role>(entity => entity.Name("AdminTestRoles"));
            modelBuilder.ConfigEntity<UserRole>(entity => entity.Name("AdminTestUserRoles"));
        }
    }
#endif
}
