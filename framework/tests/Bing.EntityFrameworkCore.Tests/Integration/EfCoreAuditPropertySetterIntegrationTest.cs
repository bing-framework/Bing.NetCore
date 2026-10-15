using System.Collections;
using System.Globalization;
using System.Reflection;
using Bing.Auditing;
using Bing.Data.Filters;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Test.Shared.Identity;
using Bing.Test.Shared.Timing;
using Bing.Timing;
using Bing.Users;

namespace Bing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// 验证 EF Core 工作单元保存时填充实体审计字段。
/// </summary>
public sealed class EfCoreAuditPropertySetterIntegrationTest
{
    /// <summary>
    /// 用于断言实体审计时间的固定时刻。
    /// </summary>
    private static readonly DateTime CreationTime = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 测试新增和修改实体时应通过真实工作单元填充并保存审计字段。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_ShouldPersistCreationAndModificationAuditFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new FakeClock(CreationTime);
        var user = FakeCurrentUser.AsAuthenticated("user-001", "测试用户");
        using var provider = CreateServiceProvider(clock, user);
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        using var auditScope = auditManager.BeginScope();

        await unitOfWork.Database.EnsureCreatedAsync();
        var entity = new AuditedSample { Name = "before", Password = "secret" };
        unitOfWork.Samples.Add(entity);

        var created = await unitOfWork.SaveChangesAsync();

        Assert.Equal(1, created);
        Assert.Equal(CreationTime, entity.CreationTime);
        Assert.Equal(CreationTime, entity.LastModificationTime);
        Assert.Equal("user-001", entity.CreatorId);
        Assert.Equal("user-001", entity.LastModifierId);
        Assert.Equal("测试用户", entity.Creator);
        Assert.Equal("测试用户", entity.LastModifier);

        var auditChange = Assert.Single(auditManager.Current.Log.EntityChanges);
        Assert.Equal(EntityChangeType.Created, auditChange.ChangeType);
        Assert.NotEqual(0, entity.Id);
        Assert.Equal(entity.Id.ToString(CultureInfo.InvariantCulture), auditChange.EntityId);
        Assert.Contains(auditChange.PropertyChanges, property =>
            property.PropertyName == nameof(AuditedSample.Name) && property.NewValue == "before");
        Assert.DoesNotContain(auditChange.PropertyChanges, property =>
            property.PropertyName.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0 ||
            property.PropertyName.IndexOf("Token", StringComparison.OrdinalIgnoreCase) >= 0);

        var nextTime = CreationTime.AddMinutes(5);
        clock.Set(nextTime);
        entity.Name = "after";

        using (var updateScope = auditManager.BeginScope())
        {
            var updated = await unitOfWork.SaveChangesAsync();

            Assert.Equal(1, updated);
            Assert.Equal(CreationTime, entity.CreationTime);
            Assert.Equal(nextTime, entity.LastModificationTime);
            Assert.Equal("user-001", entity.CreatorId);
            Assert.Equal("user-001", entity.LastModifierId);
            var updateChange = Assert.Single(auditManager.Current.Log.EntityChanges);
            Assert.Equal(EntityChangeType.Updated, updateChange.ChangeType);
            Assert.Contains(updateChange.PropertyChanges, property =>
                property.PropertyName == nameof(AuditedSample.Name) && property.OriginalValue == "before" &&
                property.NewValue == "after");
        }

        using (var deleteScope = auditManager.BeginScope())
        {
            unitOfWork.Samples.Remove(entity);
            Assert.Equal(1, await unitOfWork.SaveChangesAsync());

            var deleteChange = Assert.Single(auditManager.Current.Log.EntityChanges);
            Assert.Equal(EntityChangeType.Deleted, deleteChange.ChangeType);
            Assert.Contains(deleteChange.PropertyChanges, property =>
                property.PropertyName == nameof(AuditedSample.Name) && property.OriginalValue == "after" &&
                property.NewValue == null);
        }

        Assert.Empty(await unitOfWork.Samples.AsNoTracking().ToListAsync());
    }

    /// <summary>
    /// 验证仅截断后的文本相同但原始值不同仍记录属性变化。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenLongValueChangesAfterSnapshotLimit_ShouldRecordPropertyChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateServiceProvider(new FakeClock(CreationTime),
            FakeCurrentUser.AsAuthenticated("user-001", "测试用户"));
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        await unitOfWork.Database.EnsureCreatedAsync();
        var original = new string('x', 512) + "A";
        var current = new string('x', 512) + "B";
        var entity = new AuditedSample { Name = original };

        using (auditManager.BeginScope())
        {
            unitOfWork.Samples.Add(entity);
            await unitOfWork.SaveChangesAsync();
        }

        using (auditManager.BeginScope())
        {
            entity.Name = current;
            unitOfWork.Samples.Update(entity);
            Assert.Equal(1, await unitOfWork.SaveChangesAsync());

            var change = Assert.Single(auditManager.Current.Log.EntityChanges);
            var property = Assert.Single(change.PropertyChanges,
                item => item.PropertyName == nameof(AuditedSample.Name));
            Assert.Equal(512, property.OriginalValue.Length);
            Assert.Equal(512, property.NewValue.Length);
            Assert.Equal(new string('x', 512), property.OriginalValue);
            Assert.Equal(new string('x', 512), property.NewValue);
        }
    }

    /// <summary>
    /// 验证审计值格式化失败时仍保存业务数据并跳过异常属性。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenAuditValueFormattingThrows_ShouldKeepBusinessSaveSuccessful()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateServiceProvider(new FakeClock(CreationTime),
            FakeCurrentUser.AsAuthenticated("user-001", "测试用户"));
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        await unitOfWork.Database.EnsureCreatedAsync();
        var entity = new AuditedSample
        {
            Name = "normal",
            ThrowingValue = new ThrowingAuditValue("stored")
        };

        using (auditManager.BeginScope())
        {
            unitOfWork.Samples.Add(entity);

            Assert.Equal(1, await unitOfWork.SaveChangesAsync());

            var change = Assert.Single(auditManager.Current.Log.EntityChanges);
            Assert.Contains(change.PropertyChanges, property =>
                property.PropertyName == nameof(AuditedSample.Name) && property.NewValue == "normal");
            Assert.DoesNotContain(change.PropertyChanges,
                property => property.PropertyName == nameof(AuditedSample.ThrowingValue));
        }

        var persisted = await unitOfWork.Samples.AsNoTracking().SingleAsync();
        Assert.Equal("normal", persisted.Name);
        Assert.Equal("stored", persisted.ThrowingValue.Value);
    }

    /// <summary>
    /// 验证多个新增实体的数据库生成主键不会合并成一条审计记录。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenMultipleEntitiesUseGeneratedKeys_ShouldKeepDistinctAuditIds()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var provider = CreateServiceProvider(new FakeClock(CreationTime),
            FakeCurrentUser.AsAuthenticated("user-001", "测试用户"));
        await using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        await unitOfWork.Database.EnsureCreatedAsync();
        var first = new AuditedSample { Name = "first" };
        var second = new AuditedSample { Name = "second" };

        using (auditManager.BeginScope())
        {
            unitOfWork.Samples.AddRange(first, second);
            Assert.Equal(2, await unitOfWork.SaveChangesAsync());

            var changes = auditManager.Current.Log.EntityChanges;
            Assert.Equal(2, changes.Count);
            Assert.Contains(changes, change =>
                change.EntityId == first.Id.ToString(CultureInfo.InvariantCulture) &&
                change.PropertyChanges.Any(property => property.PropertyName == nameof(AuditedSample.Name) &&
                    property.NewValue == "first"));
            Assert.Contains(changes, change =>
                change.EntityId == second.Id.ToString(CultureInfo.InvariantCulture) &&
                change.PropertyChanges.Any(property => property.PropertyName == nameof(AuditedSample.Name) &&
                    property.NewValue == "second"));
        }
    }

    /// <summary>
    /// 验证同步保存也会刷新数据库生成的审计主键。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenGeneratedKeyIsAssigned_ShouldRefreshAuditEntityId()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = CreateServiceProvider(new FakeClock(CreationTime),
            FakeCurrentUser.AsAuthenticated("user-001", "测试用户"));
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        unitOfWork.Database.EnsureCreated();
        var entity = new AuditedSample { Name = "sync" };

        using (auditManager.BeginScope())
        {
            unitOfWork.Samples.Add(entity);
            Assert.Equal(1, unitOfWork.SaveChanges());
            var change = Assert.Single(auditManager.Current.Log.EntityChanges);
            Assert.NotEqual(0, entity.Id);
            Assert.Equal(entity.Id.ToString(CultureInfo.InvariantCulture), change.EntityId);
        }
    }

    /// <summary>
    /// 验证保存前回调失败后不会保留待刷新审计记录的实体引用。
    /// </summary>
    [Fact]
    public void SaveChanges_WhenSavePreparationFails_ShouldClearPendingAuditReferences()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var provider = CreateServiceProvider(new FakeClock(CreationTime),
            FakeCurrentUser.AsAuthenticated("user-001", "测试用户"));
        using var unitOfWork = CreateUnitOfWork(connection, provider);
        var auditManager = provider.GetRequiredService<IAuditingManager>();
        unitOfWork.Database.EnsureCreated();
        unitOfWork.Samples.Add(new AuditedSample { Name = "failed" });

        using (auditManager.BeginScope())
        {
            unitOfWork.ThrowAfterAuditCapture = true;
            var exception = Assert.Throws<InvalidOperationException>(() => unitOfWork.SaveChanges());

            Assert.Equal("测试保存准备失败。", exception.Message);
            var field = typeof(AuditUnitOfWork).BaseType?.GetField("_pendingCreatedAuditChanges",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.Empty((IEnumerable)field.GetValue(unitOfWork));
        }
    }

    /// <summary>
    /// 创建包含审计依赖的测试服务提供程序。
    /// </summary>
    private static ServiceProvider CreateServiceProvider(FakeClock clock, FakeCurrentUser user)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<IDataFilter, DataFilter>();
        services.AddScoped<IFilterManager, FilterManager>();
        services.AddScoped<IAuditPropertySetter, AuditPropertySetter>();
        services.AddScoped<IAuditingManager, AuditingManager>();
        services.AddSingleton<IAuditingStore, RecordingAuditingStore>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建使用 SQLite 连接的测试工作单元。
    /// </summary>
    private static AuditUnitOfWork CreateUnitOfWork(SqliteConnection connection, IServiceProvider provider)
    {
        var options = new DbContextOptionsBuilder<AuditUnitOfWork>()
            .UseSqlite(connection)
            .Options;
        return new AuditUnitOfWork(options, provider)
        {
            LazyServiceProvider = new LazyServiceProvider(provider)
        };
    }

    /// <summary>
    /// 使用真实 EF Core SQLite 保存审计实体的测试工作单元。
    /// </summary>
    private sealed class AuditUnitOfWork : UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="AuditUnitOfWork" /> 类型的实例。
        /// </summary>
        public AuditUnitOfWork(DbContextOptions options, IServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }

        /// <summary>
        /// 获取审计样本集合。
        /// </summary>
        public DbSet<AuditedSample> Samples { get; set; }

        /// <summary>
        /// 是否在保存前审计捕获后模拟失败。
        /// </summary>
        public bool ThrowAfterAuditCapture { get; set; }

        /// <inheritdoc />
        protected override void SaveChangesBefore()
        {
            base.SaveChangesBefore();
            if (ThrowAfterAuditCapture)
                throw new InvalidOperationException("测试保存准备失败。");
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 值对象通过转换器映射为标量，不作为独立实体参与模型发现。
            modelBuilder.Ignore<ThrowingAuditValue>();
            modelBuilder.Entity<AuditedSample>(builder =>
            {
                builder.ToTable("audit_samples");
                builder.HasKey(entity => entity.Id);
                builder.Property(entity => entity.ThrowingValue)
                    .HasConversion(value => value.Value, value => new ThrowingAuditValue(value));
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    /// <summary>
    /// 包含创建和修改审计字段的测试实体。
    /// </summary>
    private sealed class AuditedSample : IAuditedObject<string>, IHasCreator, IHasModifier
    {
        /// <summary>
        /// 获取或设置实体标识。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置实体名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置不应进入审计明细的密码。
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 获取或设置格式化时会抛出异常的值对象。
        /// </summary>
        public ThrowingAuditValue ThrowingValue { get; set; }

        /// <inheritdoc />
        public DateTime? CreationTime { get; set; }

        /// <inheritdoc />
        public string CreatorId { get; set; }

        /// <inheritdoc />
        public string Creator { get; set; }

        /// <inheritdoc />
        public DateTime? LastModificationTime { get; set; }

        /// <inheritdoc />
        public string LastModifierId { get; set; }

        /// <inheritdoc />
        public string LastModifier { get; set; }
    }

    /// <summary>
    /// 用于验证审计格式化容错的值对象。
    /// </summary>
    private sealed class ThrowingAuditValue
    {
        /// <summary>
        /// 初始化一个 <see cref="ThrowingAuditValue" /> 类型的实例。
        /// </summary>
        /// <param name="value">持久化值。</param>
        public ThrowingAuditValue(string value)
        {
            Value = value;
        }

        /// <summary>
        /// 获取持久化值。
        /// </summary>
        public string Value { get; }

        /// <inheritdoc />
        public override string ToString() => throw new InvalidOperationException("测试值对象不可格式化。");
    }

    /// <summary>
    /// 接收测试审计日志而不写入外部存储。
    /// </summary>
    private sealed class RecordingAuditingStore : IAuditingStore
    {
        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo) => Task.CompletedTask;
    }
}
