#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Repositories;
using Bing.Admin.Data;
using Bing.Admin.Data.Repositories.Commons;
using Bing.Admin.Data.UnitOfWorks.MySql;
using Bing.DependencyInjection;
using Bing.Data.Transaction;
using Bing.Admin.Service.FileStorage;
using Bing.MultiTenancy;
using Bing.Security.Claims;
using Bing.Biz.FileManagement;
using Bing.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#if BING_ADMIN_EFCORE_TESTS
using Bing.Auditing;
using Bing.Timing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Bing.Uow;
namespace Bing.Admin.EFCore.Tests;
#else
using Bing.Data.Sql;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Dapper;
using Bing.FreeSQL;
using FreeSql;
namespace Bing.Admin.FreeSQL.Tests;
#endif

/// <summary>通过真实 SQLite UserInfo 仓储验证资料附件授权。</summary>
internal sealed class AdminUserInfoPersistenceTestEnvironment : IAsyncDisposable
{
#if BING_ADMIN_EFCORE_TESTS
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AdminUnitOfWork> _options;
#else
    private readonly IFreeSql _orm;
    private readonly FreeSqlWrapper _wrapper;
#endif
    private readonly ServiceProvider _serviceProvider;
    private readonly AdminUserInfoTestUnitOfWork _unitOfWork;

    public AdminUserInfoPersistenceTestEnvironment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ILazyServiceProvider, LazyServiceProvider>();
        services.AddScoped<ITransactionActionManager, TransactionActionManager>();
#if BING_ADMIN_EFCORE_TESTS
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AdminUnitOfWork>().UseSqlite(_connection).Options;
        services.AddTransient<IAuditPropertySetter, AuditPropertySetter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(NullCurrentUser.Instance);
        _serviceProvider = services.BuildServiceProvider();
        _unitOfWork = new AdminUserInfoTestUnitOfWork(_options, _serviceProvider)
        {
            LazyServiceProvider = new LazyServiceProvider(_serviceProvider)
        };
        Repository = new UserInfoRepository(_unitOfWork);
#else
        services.AddSqlCore();
        _orm = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, "Data Source=:memory:")
            .Build();
        _wrapper = new FreeSqlWrapper { Orm = _orm };
        services.AddSingleton(_orm);
        services.AddSingleton(_wrapper);
        _serviceProvider = services.BuildServiceProvider();
        _unitOfWork = new AdminUserInfoTestUnitOfWork(_wrapper, _serviceProvider)
        {
            LazyServiceProvider = new LazyServiceProvider(_serviceProvider)
        };
        Repository = new UserInfoRepository(_unitOfWork,
            _serviceProvider.GetRequiredService<ISqlQueryFactory>(),
            _serviceProvider.GetRequiredService<IDatabaseContextAccessor>(),
            _serviceProvider.GetRequiredService<SqlMetadataOptions>(),
            _serviceProvider.GetRequiredService<ITypeConverterResolver>());
#endif
    }

    public IUserInfoRepository Repository { get; }

    public void Configure(IServiceCollection services, ICurrentPrincipalAccessor principalAccessor)
    {
        services.RemoveAll<ICurrentPrincipalAccessor>();
        services.AddSingleton<ICurrentPrincipalAccessor>(principalAccessor);
        services.RemoveAll<ICurrentUser>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.RemoveAll<ICurrentTenant>();
        services.AddScoped<ICurrentTenant, AdminFileCurrentTenant>();
        services.RemoveAll<IUserInfoRepository>();
        services.AddSingleton(Repository);
        services.RemoveAll<IFileAccessAuthorizer>();
        services.AddScoped<IFileAccessAuthorizer, AdminUserInfoFileAuthorizer>();
    }

    public async Task InitializeAsync(IEnumerable<UserInfo> userInfos)
    {
#if BING_ADMIN_EFCORE_TESTS
        await _unitOfWork.Database.EnsureCreatedAsync();
        await _unitOfWork.Set<UserInfo>().AddRangeAsync(userInfos);
        await _unitOfWork.SaveChangesAsync();
#else
        _ = _unitOfWork.Set<UserInfo>();
        _orm.CodeFirst.SyncStructure<UserInfo>();
        _orm.Insert(userInfos).ExecuteAffrows();
#endif
    }

    public ValueTask DisposeAsync()
    {
        _unitOfWork.Dispose();
        _serviceProvider.Dispose();
#if BING_ADMIN_EFCORE_TESTS
        _connection.Dispose();
#else
        _orm.Dispose();
#endif
        return ValueTask.CompletedTask;
    }

#if BING_ADMIN_EFCORE_TESTS
    private sealed class AdminUserInfoTestUnitOfWork : AdminUnitOfWork
    {
        public AdminUserInfoTestUnitOfWork(DbContextOptions<AdminUnitOfWork> options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserInfo>(entity =>
            {
                entity.ToTable("AdminTestUserInfos");
                entity.HasKey(info => info.Id);
                entity.Property(info => info.Id).HasColumnName("UserId");
                entity.HasQueryFilter(info => !info.IsDeleted);
            });
        }
    }
#else
    private sealed class AdminUserInfoTestUnitOfWork : AdminUnitOfWork
    {
        public AdminUserInfoTestUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider serviceProvider)
            : base(wrapper, serviceProvider)
        {
        }

        protected override void OnModelCreating(ICodeFirst modelBuilder)
        {
            modelBuilder.ConfigEntity<UserInfo>(entity => entity.Name("AdminTestUserInfos"));
        }
    }
#endif
}
