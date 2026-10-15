using System.Globalization;
using System.Threading;
using Bing.Auditing;
using Bing.Data.Transaction;
using Bing.FreeSQL;
using FreeSql;
using FreeSql.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FreeSQL.Tests.Core;

/// <summary>
/// 验证 FreeSQL 工作单元通过真实 SQLite 保存生成安全实体审计快照。
/// </summary>
public sealed class UnitOfWorkEntityAuditSnapshotTest
{
    /// <summary>
    /// 验证创建、更新和删除均捕获正确前后值，并排除敏感及未映射属性。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenEntityChanges_ShouldCaptureCreatedUpdatedAndDeletedSnapshots(bool asynchronous)
    {
        using var fixture = new AuditFixture();
        var entity = Sample();
        using (fixture.Manager.BeginScope())
        {
            fixture.Unit.Set<AuditSample>().Add(entity);
            Assert.Equal(1, await SaveAsync(fixture.Unit, asynchronous));
            var created = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
            Assert.Equal(EntityChangeType.Created, created.ChangeType);
            Assert.Equal(entity.Id.ToString(CultureInfo.InvariantCulture), created.EntityId);
            Assert.Equal("tenant", created.EntityTenantId);
            Assert.Equal(typeof(AuditSample).FullName, created.EntityTypeFullName);
            AssertProperty(created, nameof(AuditSample.Name), null, "before");
            AssertProperty(created, nameof(AuditSample.Amount), null, "1234.5");
            AssertProperty(created, nameof(AuditSample.Description), null, new string('x', 512));
            Assert.DoesNotContain(created.PropertyChanges, property => IsExcluded(property.PropertyName));
            entity.Name = "mutated-after-save";
            AssertProperty(created, nameof(AuditSample.Name), null, "before");
        }
        using (fixture.Manager.BeginScope())
        {
            entity.Name = "after";
            fixture.Unit.Set<AuditSample>().Update(entity);
            Assert.Equal(1, await SaveAsync(fixture.Unit, asynchronous));
            var updated = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
            Assert.Equal(EntityChangeType.Updated, updated.ChangeType);
            AssertProperty(updated, nameof(AuditSample.Name), "before", "after");
            Assert.DoesNotContain(updated.PropertyChanges, property => property.PropertyName == nameof(AuditSample.Amount));
            Assert.DoesNotContain(updated.PropertyChanges, property => IsExcluded(property.PropertyName));
        }
        using (fixture.Manager.BeginScope())
        {
            fixture.Unit.Set<AuditSample>().Remove(entity);
            Assert.Equal(1, await SaveAsync(fixture.Unit, asynchronous));
            var deleted = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
            Assert.Equal(EntityChangeType.Deleted, deleted.ChangeType);
            AssertProperty(deleted, nameof(AuditSample.Name), "after", null);
            AssertProperty(deleted, nameof(AuditSample.Description), new string('x', 512), null);
            Assert.DoesNotContain(deleted.PropertyChanges, property => IsExcluded(property.PropertyName));
        }
        Assert.Equal(0, fixture.Orm.Select<AuditSample>().Count());
    }

    /// <summary>
    /// 验证仅截断后的文本相同但原始值不同仍记录属性变化。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenLongValueChangesAfterSnapshotLimit_ShouldRecordPropertyChange()
    {
        using var fixture = new AuditFixture();
        var original = new string('x', 512) + "A";
        var current = new string('x', 512) + "B";
        var entity = Sample();
        entity.Name = original;
        fixture.Unit.Set<AuditSample>().Add(entity);
        await fixture.Unit.SaveChangesAsync();

        using var scope = fixture.Manager.BeginScope();
        entity.Name = current;
        fixture.Unit.Set<AuditSample>().Update(entity);
        Assert.Equal(1, await fixture.Unit.SaveChangesAsync());

        var change = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
        var property = Assert.Single(change.PropertyChanges,
            item => item.PropertyName == nameof(AuditSample.Name));
        Assert.Equal(512, property.OriginalValue.Length);
        Assert.Equal(512, property.NewValue.Length);
        Assert.Equal(new string('x', 512), property.OriginalValue);
        Assert.Equal(new string('x', 512), property.NewValue);
    }

    /// <summary>
    /// 验证无活动审计作用域不安装采集回调，也不改变宿主实体变化通知。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenNoAuditScope_ShouldPreserveHostCallbackAndSkipCapture()
    {
        using var fixture = new AuditFixture();
        var calls = 0;
        Action<List<DbContext.EntityChangeReport.ChangeInfo>> callback = _ => calls++;
        fixture.Unit.SetReportCallback(callback);
        var entity = Sample();
        fixture.Unit.Set<AuditSample>().Add(entity);
        await fixture.Unit.SaveChangesAsync();
        Assert.Null(fixture.Manager.Current);
        Assert.Same(callback, fixture.Unit.GetReportCallback());
        Assert.Equal(1, calls);
        AuditLogInfo log;
        using (fixture.Manager.BeginScope())
        {
            entity.Name = "audited";
            fixture.Unit.Set<AuditSample>().Update(entity);
            await fixture.Unit.SaveChangesAsync();
            log = fixture.Manager.Current.Log;
            AssertProperty(Assert.Single(log.EntityChanges), nameof(AuditSample.Name), "before", "audited");
        }
        entity.Name = "outside";
        fixture.Unit.Set<AuditSample>().Update(entity);
        await fixture.Unit.SaveChangesAsync();
        Assert.Null(fixture.Manager.Current);
        Assert.Equal(3, calls);
        AssertProperty(Assert.Single(log.EntityChanges), nameof(AuditSample.Name), "before", "audited");
    }

    /// <summary>
    /// 验证宿主报告或配置回调继续执行，重复保存合并首个旧值和最后新值。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChangesAsync_WhenHostCallbackExists_ShouldPreserveItAndMergeRepeatedUpdates(bool reportCallback)
    {
        using var fixture = new AuditFixture();
        var entity = Sample();
        fixture.Unit.Set<AuditSample>().Add(entity);
        await fixture.Unit.SaveChangesAsync();
        var calls = 0;
        Action<List<DbContext.EntityChangeReport.ChangeInfo>> callback = _ => calls++;
        if (reportCallback)
            fixture.Unit.SetReportCallback(callback);
        else
            fixture.Unit.Options.OnEntityChange = callback;
        using var scope = fixture.Manager.BeginScope();
        entity.Name = "middle";
        fixture.Unit.Set<AuditSample>().Update(entity);
        await fixture.Unit.SaveChangesAsync();
        entity.Name = "final";
        fixture.Unit.Set<AuditSample>().Update(entity);
        await fixture.Unit.SaveChangesAsync();
        Assert.Equal(2, calls);
        var updated = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
        Assert.Equal(EntityChangeType.Updated, updated.ChangeType);
        AssertProperty(updated, nameof(AuditSample.Name), "before", "final");
        Assert.Empty(fixture.Unit.GetReportEntries());
        Assert.Equal("final", fixture.Orm.Select<AuditSample>().First().Name);
    }

    /// <summary>
    /// 验证只有敏感字段变化时不生成属性快照，也不将敏感值放入内存日志。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenOnlySensitivePropertiesChange_ShouldNotAppendEntityChanges()
    {
        using var fixture = new AuditFixture();
        var entity = Sample();
        fixture.Unit.Set<AuditSample>().Add(entity);
        await fixture.Unit.SaveChangesAsync();
        using var scope = fixture.Manager.BeginScope();
        entity.Password = "changed-password";
        entity.AccessToken = "changed-token";
        entity.SecretValue = "changed-secret";
        entity.Credential = "changed-credential";
        fixture.Unit.Set<AuditSample>().Update(entity);
        Assert.Equal(1, await fixture.Unit.SaveChangesAsync());
        Assert.Empty(fixture.Manager.Current.Log.EntityChanges);
        Assert.Equal("changed-password", fixture.Orm.Select<AuditSample>().First().Password);
    }

    /// <summary>
    /// 验证审计快照在不同线程区域设置下使用固定数值格式。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenCultureUsesDecimalComma_ShouldFormatValuesInvariantly()
    {
        using var fixture = new AuditFixture();
        using var scope = fixture.Manager.BeginScope();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            fixture.Unit.Set<AuditSample>().Add(Sample());
            await fixture.Unit.SaveChangesAsync();
            AssertProperty(Assert.Single(fixture.Manager.Current.Log.EntityChanges), nameof(AuditSample.Amount), null, "1234.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// 验证属性 getter 在真实提交后失败不会误报保存失败，其他安全属性仍可采集。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveChanges_WhenAuditGetterThrowsAfterCommit_ShouldKeepBusinessSuccess(bool asynchronous)
    {
        using var fixture = new AuditFixture();
        using var scope = fixture.Manager.BeginScope();
        var probe = new AuditGetterProbe();
        var previous = AuditSample.GetterProbe.Value;
        AuditSample.GetterProbe.Value = probe;
        var committed = 0;
        var hostCalls = 0;
        fixture.Unit.SetReportCallback(_ => hostCalls++);
        fixture.Orm.Aop.TraceAfter += (_, args) =>
        {
            if (args.Remark == "提交" && args.Exception == null)
            {
                committed++;
                probe.Throw = true;
            }
        };
        try
        {
            fixture.Unit.Set<AuditSample>().Add(Sample());
            Assert.Equal(1, await SaveAsync(fixture.Unit, asynchronous));
            Assert.Equal(1, committed);
            Assert.True(probe.Failures > 0);
            Assert.Equal(1, hostCalls);
            var change = Assert.Single(fixture.Manager.Current.Log.EntityChanges);
            AssertProperty(change, nameof(AuditSample.Name), null, "before");
            Assert.DoesNotContain(change.PropertyChanges, property => property.PropertyName == nameof(AuditSample.Description));
            probe.Throw = false;
            var persisted = fixture.Orm.Select<AuditSample>().First();
            Assert.Equal("before", persisted.Name);
            Assert.Equal(new string('x', 700), persisted.Description);
        }
        finally
        {
            AuditSample.GetterProbe.Value = previous;
        }
    }

    /// <summary>
    /// 验证仅隔离自动采集错误，宿主原有通知异常仍按原生语义传播。
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WhenHostCallbackThrows_ShouldPreserveOriginalError()
    {
        using var fixture = new AuditFixture();
        using var scope = fixture.Manager.BeginScope();
        var original = new ApplicationException("host callback failed");
        fixture.Unit.SetReportCallback(_ => throw original);
        fixture.Unit.Set<AuditSample>().Add(Sample());
        Assert.Same(original, await Assert.ThrowsAsync<ApplicationException>(() => fixture.Unit.SaveChangesAsync()));
        Assert.Equal(1, fixture.Orm.Select<AuditSample>().Count());
        AssertProperty(Assert.Single(fixture.Manager.Current.Log.EntityChanges), nameof(AuditSample.Name), null, "before");
    }

    /// <summary>
    /// 使用指定保存入口执行真实 SQLite 实体保存。
    /// </summary>
    /// <param name="unit">当前测试工作单元。</param>
    /// <param name="asynchronous">是否选择异步保存入口。</param>
    /// <returns>当前保存影响的实体行数。</returns>
    private static Task<int> SaveAsync(AuditUnitOfWork unit, bool asynchronous) => asynchronous
        ? unit.SaveChangesAsync()
        : Task.FromResult(unit.SaveChanges());

    /// <summary>
    /// 断言指定属性只有一条快照且前后值完全匹配。
    /// </summary>
    /// <param name="change">实体变化快照。</param>
    /// <param name="name">属性名称。</param>
    /// <param name="original">预期原始值。</param>
    /// <param name="current">预期新值。</param>
    private static void AssertProperty(EntityChangeInfo change, string name, string original, string current)
    {
        var property = Assert.Single(change.PropertyChanges, item => item.PropertyName == name);
        Assert.Equal(original, property.OriginalValue);
        Assert.Equal(current, property.NewValue);
    }

    /// <summary>
    /// 判断测试模型中的敏感或未映射字段。
    /// </summary>
    /// <param name="name">已采集的属性名称。</param>
    /// <returns>属于应排除的字段时返回 true，否则返回 false。</returns>
    private static bool IsExcluded(string name) => name == nameof(AuditSample.Password) ||
        name == nameof(AuditSample.AccessToken) || name == nameof(AuditSample.SecretValue) ||
        name == nameof(AuditSample.Credential) || name == nameof(AuditSample.Unmapped);

    /// <summary>
    /// 创建包含长文本和敏感字段的独立样本。
    /// </summary>
    /// <returns>供当前真实保存操作使用的实体。</returns>
    private static AuditSample Sample() => new()
    {
        Id = 1,
        TenantId = "tenant",
        Name = "before",
        Amount = 1234.5m,
        Description = new string('x', 700),
        Password = "password-sensitive",
        AccessToken = "token-sensitive",
        SecretValue = "secret-sensitive",
        Credential = "credential-sensitive",
        Unmapped = "not-mapped"
    };

    /// <summary>
    /// 拥有独立临时 SQLite 数据库与真实审计管理器。
    /// </summary>
    private sealed class AuditFixture : IDisposable
    {
        /// <summary>
        /// 保存当前用例独占的数据库文件路径。
        /// </summary>
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"bing-entity-audit-{Guid.NewGuid():N}.db");

        /// <summary>
        /// 保存测试服务容器的释放所有权。
        /// </summary>
        private readonly ServiceProvider _provider;

        /// <summary>
        /// 获取独立 SQLite ORM。
        /// </summary>
        internal IFreeSql Orm { get; }

        /// <summary>
        /// 获取真实审计管理器。
        /// </summary>
        internal IAuditingManager Manager { get; }

        /// <summary>
        /// 获取真实 FreeSQL 框架工作单元。
        /// </summary>
        internal AuditUnitOfWork Unit { get; }

        /// <summary>
        /// 初始化一个 <see cref="AuditFixture" /> 类型的实例。
        /// </summary>
        internal AuditFixture()
        {
            Orm = new FreeSqlBuilder().UseConnectionString(DataType.Sqlite, $"Data Source={_path}").Build();
            Orm.CodeFirst.SyncStructure<AuditSample>();
            var services = new ServiceCollection();
            services.AddSingleton<ITransactionActionManager, TransactionActionManager>();
            services.AddSingleton<IAuditingManager>(new AuditingManager(new RecordingAuditingStore()));
            _provider = services.BuildServiceProvider();
            Manager = _provider.GetRequiredService<IAuditingManager>();
            Unit = new AuditUnitOfWork(new FreeSqlWrapper { Orm = Orm }, _provider);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Unit.Dispose();
            _provider.Dispose();
            Orm.Dispose();
            File.Delete(_path);
        }
    }

    /// <summary>
    /// 暴露报告回调以验证真实 FreeSQL 通知生命周期。
    /// </summary>
    private sealed class AuditUnitOfWork : Bing.Uow.UnitOfWorkBase
    {
        /// <summary>
        /// 初始化一个 <see cref="AuditUnitOfWork" /> 类型的实例。
        /// </summary>
        /// <param name="wrapper">测试 ORM 包装。</param>
        /// <param name="provider">包含审计与事务服务的容器。</param>
        internal AuditUnitOfWork(FreeSqlWrapper wrapper, IServiceProvider provider) : base(wrapper, provider)
        {
        }

        /// <summary>
        /// 设置宿主实体变化通知。
        /// </summary>
        /// <param name="callback">原生报告通知回调。</param>
        internal void SetReportCallback(Action<List<DbContext.EntityChangeReport.ChangeInfo>> callback) =>
            UnitOfWork.EntityChangeReport.OnChange = callback;

        /// <summary>
        /// 获取当前原生报告通知回调。
        /// </summary>
        /// <returns>当前工作单元报告上的通知委托。</returns>
        internal Action<List<DbContext.EntityChangeReport.ChangeInfo>> GetReportCallback() => UnitOfWork.EntityChangeReport.OnChange;

        /// <summary>
        /// 获取原生报告清理后的实体变化集合。
        /// </summary>
        /// <returns>当前物理工作单元拥有的报告集合。</returns>
        internal List<DbContext.EntityChangeReport.ChangeInfo> GetReportEntries() => UnitOfWork.EntityChangeReport.Report;
    }

    /// <summary>
    /// 接收测试审计日志而不访问外部存储。
    /// </summary>
    private sealed class RecordingAuditingStore : IAuditingStore
    {
        /// <inheritdoc />
        public Task SaveAsync(AuditLogInfo auditInfo) => Task.CompletedTask;
    }

    /// <summary>
    /// 控制属性读取仅在当前用例真实提交完成后失败。
    /// </summary>
    private sealed class AuditGetterProbe
    {
        /// <summary>
        /// 获取或设置当前执行流是否已进入提交后采集阶段。
        /// </summary>
        internal bool Throw { get; set; }

        /// <summary>
        /// 获取或设置提交后属性读取失败次数。
        /// </summary>
        internal int Failures { get; set; }

        /// <summary>
        /// 获取本次属性读取抛出的固定测试异常。
        /// </summary>
        internal Exception Error { get; } = new ApplicationException("audit getter failed");
    }

    /// <summary>
    /// 定义真实 SQLite 保存的审计属性与应排除字段。
    /// </summary>
    [Table(Name = "audit_snapshot_samples")]
    private sealed class AuditSample
    {
        /// <summary>
        /// 保存当前异步执行流共享的属性故障探针，避免影响其他用例。
        /// </summary>
        internal static AsyncLocal<AuditGetterProbe> GetterProbe { get; } = new();

        /// <summary>
        /// 保存说明文本，故障探针只影响读取而不影响映射赋值。
        /// </summary>
        private string _description;

        /// <summary>
        /// 获取或设置稳定的测试主键。
        /// </summary>
        [Column(IsPrimary = true)]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置实体租户。
        /// </summary>
        public string TenantId { get; set; }

        /// <summary>
        /// 获取或设置业务名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 获取或设置用于固定区域格式验证的金额。
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 获取或设置超过审计长度上限的说明。
        /// </summary>
        [Column(StringLength = -1)]
        public string Description
        {
            get
            {
                var probe = GetterProbe.Value;
                if (probe?.Throw == true)
                {
                    probe.Failures++;
                    throw probe.Error;
                }
                return _description;
            }
            set => _description = value;
        }

        /// <summary>
        /// 获取或设置应排除的密码。
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 获取或设置应排除的访问令牌。
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// 获取或设置应排除的秘密。
        /// </summary>
        public string SecretValue { get; set; }

        /// <summary>
        /// 获取或设置应排除的凭据。
        /// </summary>
        public string Credential { get; set; }

        /// <summary>
        /// 获取或设置不参与数据库映射的值。
        /// </summary>
        [Column(IsIgnore = true)]
        public string Unmapped { get; set; }
    }
}
