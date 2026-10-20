using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Reflection;
using Bing.Auditing;
using Bing.Aspects;
using Bing.Data;
using Bing.Data.Sql.Metadata;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities.Events;
using Bing.Exceptions;
using Bing.FreeSQL;
using Bing.Users;
using FreeSql;
using FreeSql.Internal.CommonProvider;
using AuditEntityChangeType = Bing.Auditing.EntityChangeType;

namespace Bing.Uow;

/// <summary>
/// 工作单元
/// </summary>
public abstract class UnitOfWorkBase : DbContext, Bing.Uow.IUnitOfWork, IDatabase
{
    #region 字段

    /// <summary>
    /// 映射字典
    /// </summary>
    private static readonly ConcurrentDictionary<Type, IEnumerable<IMap>> _maps;

    /// <summary>
    /// 服务提供器
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 当前工作单元显式登记的领域事件源。
    /// </summary>
    private readonly List<IHasDomainEvents> _domainEventSources = new();

    /// <summary>
    /// 记录已经接入自动审计的实体变化报告。
    /// </summary>
    private DbContext.EntityChangeReport _auditChangeReport;

    /// <summary>
    /// 保存本工作单元安装的复合实体变化回调，避免重复接线。
    /// </summary>
    private Action<List<DbContext.EntityChangeReport.ChangeInfo>> _auditChangeCallback;

    #endregion

    #region 属性

    /// <summary>
    /// 获取或设置跟踪号
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// 获取或设置Lazy延迟加载服务提供程序
    /// </summary>
    [Autowired]
    public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

    /// <summary>
    /// 获取当前用户
    /// </summary>
    protected ICurrentUser CurrentUser => LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();

    /// <summary>
    /// 获取当前工作单元所属的服务提供程序。
    /// </summary>
    internal IServiceProvider ServiceProvider => _serviceProvider;

    #endregion

    #region 静态构造函数

    /// <summary>
    /// 初始化一个<see cref="UnitOfWorkBase"/>类型的静态实例
    /// </summary>
    static UnitOfWorkBase()
    {
        _maps = new ConcurrentDictionary<Type, IEnumerable<IMap>>();
    }

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化一个 <see cref="UnitOfWorkBase" /> 类型的实例。
    /// </summary>
    /// <param name="wrapper">FreeSQL包装器</param>
    /// <param name="serviceProvider">服务提供程序</param>
    protected UnitOfWorkBase(FreeSqlWrapper wrapper,  IServiceProvider serviceProvider) : base(wrapper.Orm, null)
    {
        TraceId = Guid.NewGuid().ToString();
        _serviceProvider = serviceProvider ?? ServiceLocator.Instance.GetService<IServiceProvider>();
        RegisterToManager();
    }

    /// <summary>
    /// 注册到工作单元管理器
    /// </summary>
    private void RegisterToManager()
    {
        var manager = Create<IUnitOfWorkManager>();
        manager?.Register(this);
    }

    /// <summary>
    /// 创建实例
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <returns>从服务提供程序解析的实例；未解析到服务时返回默认值。</returns>
    private T Create<T>()
    {
        var result = _serviceProvider.GetService(typeof(T));
        if (result == null)
            return default;
        return (T)result;
    }

    /// <summary>
    /// 登记由当前工作单元保存的领域事件源。
    /// </summary>
    /// <param name="source">领域事件源。</param>
    public void RegisterDomainEventSource(IHasDomainEvents source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!_domainEventSources.Any(existing => ReferenceEquals(existing, source)))
            _domainEventSources.Add(source);
    }

    /// <summary>
    /// 为本次保存登记事务内领域事件分发。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器。</param>
    /// <returns>有待分发事件时返回对应会话。</returns>
    private DomainEventDispatchSession RegisterPendingDomainEvents(ITransactionActionManager transactionActionManager)
    {
        _domainEventSources.RemoveAll(source => source.GetDomainEvents()?.Count is not > 0);
        if (_domainEventSources.Count == 0)
            return null;

        var dispatcher = Create<IDomainEventDispatcher>();
        if (dispatcher == null)
            return null;

        if (UnitOfWork.GetOrBeginTransaction(false) != null)
            throw new InvalidOperationException("领域事件分发不能接入已开启的外部 FreeSQL 事务。");

        var session = new DomainEventDispatchSession(() => _domainEventSources.ToArray(), dispatcher);
        transactionActionManager.Register(_ => session.DispatchAsync());
        return session;
    }

    /// <summary>
    /// 完成已分发领域事件的清理。
    /// </summary>
    /// <param name="session">本次保存的分发会话。</param>
    private void CompleteDomainEvents(DomainEventDispatchSession session)
    {
        try
        {
            session?.Complete();
            _domainEventSources.RemoveAll(source => source.GetDomainEvents()?.Count is not > 0);
        }
        catch (Exception exception)
        {
            throw new PostCommitNotificationException(exception, TransactionOutcome.Committed);
        }
    }

    #endregion

    #region OnConfiguring(配置)

    /// <inheritdoc />
    /// <remarks>
    /// 配置当前工作单元使用的 FreeSql CodeFirst 映射。
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder builder) => base.OnConfiguring(builder);

    #endregion

    #region OnModelCreating(配置映射)

    /// <inheritdoc />
    protected override void OnModelCreating(ICodeFirst modelBuilder)
    {
        var mappers = GetMaps();
        foreach (var mapper in mappers)
            mapper.Map(modelBuilder);
    }

    /// <summary>
    /// 获取映射配置列表
    /// </summary>
    /// <returns>当前工作单元对应的映射配置集合。</returns>
    private IEnumerable<IMap> GetMaps() => _maps.GetOrAdd(GetMapType(), GetMapsFromAssemblies());

    /// <summary>
    /// 获取映射接口类型
    /// </summary>
    /// <returns>用于映射缓存的类型。</returns>
    protected virtual Type GetMapType() => this.GetType();

    /// <summary>
    /// 从程序集获取映射配置列表
    /// </summary>
    /// <returns>当前程序集集合中的映射配置集合。</returns>
    private IEnumerable<IMap> GetMapsFromAssemblies()
    {
        var result = new List<IMap>();
        foreach (var assembly in GetAssemblies())
            result.AddRange(GetMapInstances(assembly));
        return result;
    }

    /// <summary>
    /// 获取映射实例列表
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>指定程序集中的映射实例集合。</returns>
    protected virtual IEnumerable<IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);

    /// <summary>
    /// 获取定义映射配置的程序集列表
    /// </summary>
    /// <returns>包含映射配置的程序集数组。</returns>
    protected virtual Assembly[] GetAssemblies() => new[] {GetType().Assembly};

    #endregion

    #region Commit(提交)

    /// <inheritdoc />
    public int Commit()
    {
        try
        {
            return SaveChanges();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("【行级乐观锁】"))
                throw new ConcurrencyException(ex);
            throw;
        }
    }

    #endregion

    #region CommitAsync(异步提交)

    /// <inheritdoc />
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("【行级乐观锁】"))
                throw new ConcurrencyException(ex);
            throw;
        }
    }

    #endregion

    #region SaveChanges(保存更改)

    /// <inheritdoc />
    public override int SaveChanges()
    {
        RegisterAuditChangeCallback();
        var transactionActionManager = Create<ITransactionActionManager>();
        var domainEventSession = RegisterPendingDomainEvents(transactionActionManager);
        int result;
        try
        {
            result = transactionActionManager.Count == 0
                ? base.SaveChanges()
                : TransactionCommit(transactionActionManager);
        }
        catch (Exception exception) when (exception.GetTransactionOutcome() == TransactionOutcome.Committed)
        {
            domainEventSession?.CompleteAfterCommitFailure(exception);
            throw;
        }
        CompleteDomainEvents(domainEventSession);
        NotifyAfterSave();
        return result;
    }

    /// <summary>
    /// 提交事务并保存更改。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器。</param>
    /// <returns>保存更改影响的行数。</returns>
    protected virtual int TransactionCommit(ITransactionActionManager transactionActionManager)
    {
        var transaction = UnitOfWork.GetOrBeginTransaction();
        int result;
        try
        {
            transactionActionManager.CommitAsync(transaction).GetAwaiter().GetResult();
            result = SaveChangesCoreAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            try
            {
                UnitOfWork.Rollback();
            }
            catch (Exception rollbackException)
            {
                var failure = new AggregateException(exception, rollbackException);
                failure.SetTransactionOutcome(TransactionOutcome.Unknown);
                throw failure;
            }
            exception.SetTransactionOutcome(TransactionOutcome.RolledBack);
            throw;
        }

        try
        {
            UnitOfWork.Commit();
        }
        catch (Exception exception)
        {
            exception.SetTransactionOutcome(TransactionOutcome.Unknown);
            throw;
        }
        return result;
    }

    #endregion

    #region SaveChangesAsync(异步保存更改)

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RegisterAuditChangeCallback();
        var transactionActionManager = Create<ITransactionActionManager>();
        var domainEventSession = RegisterPendingDomainEvents(transactionActionManager);
        int result;
        try
        {
            result = transactionActionManager.Count == 0
                ? await base.SaveChangesAsync(cancellationToken)
                : await TransactionCommit(transactionActionManager, cancellationToken);
        }
        catch (Exception exception) when (exception.GetTransactionOutcome() == TransactionOutcome.Committed)
        {
            domainEventSession?.CompleteAfterCommitFailure(exception);
            throw;
        }
        CompleteDomainEvents(domainEventSession);
        await NotifyAfterSaveAsync();
        return result;
    }

    /// <summary>
    /// 提交事务并保存更改。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含保存更改影响行数的异步任务。</returns>
    protected virtual async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transaction = UnitOfWork.GetOrBeginTransaction();
        int result;
        try
        {
            await transactionActionManager.CommitAsync(transaction);
            cancellationToken.ThrowIfCancellationRequested();
            result = await SaveChangesCoreAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                UnitOfWork.Rollback();
            }
            catch (Exception rollbackException)
            {
                var failure = new AggregateException(exception, rollbackException);
                failure.SetTransactionOutcome(TransactionOutcome.Unknown);
                throw failure;
            }
            exception.SetTransactionOutcome(TransactionOutcome.RolledBack);
            throw;
        }

        try
        {
            UnitOfWork.Commit();
        }
        catch (Exception exception)
        {
            exception.SetTransactionOutcome(TransactionOutcome.Unknown);
            throw;
        }
        return result;
    }

    #endregion

    /// <summary>
    /// 将自动审计接入当前 FreeSQL 实体变化报告并保留宿主回调。
    /// </summary>
    /// <remarks>
    /// 没有活动日志时不安装回调。FreeSQL 在保存成功或物理提交后通知实体变化，通知后清空报告；本回调立即复制安全文本。
    /// 自动属性快照不替代业务事务内的关键审计持久化。
    /// </remarks>
    private void RegisterAuditChangeCallback()
    {
        if (Create<IAuditingManager>()?.Current?.Log == null)
            return;
        var report = UnitOfWork?.EntityChangeReport;
        if (report == null)
            return;
        if (ReferenceEquals(report, _auditChangeReport) && report.OnChange?.GetInvocationList()
                .Any(callback => callback.Equals(_auditChangeCallback)) == true)
            return;
        var previous = report.OnChange ?? Options.OnEntityChange;
        _auditChangeCallback = changes =>
        {
            CaptureAuditChanges(changes);
            previous?.Invoke(changes);
        };
        _auditChangeReport = report;
        report.OnChange = _auditChangeCallback;
    }

    /// <summary>
    /// 将已保存实体变化转换为活动审计日志中的有界属性快照。
    /// </summary>
    /// <param name="changes">FreeSQL 当前通知的实体变化集合。</param>
    /// <remarks>自动快照采用尽力采集；通知可能发生在物理提交后，采集错误不能改变业务保存结果。</remarks>
    private void CaptureAuditChanges(List<DbContext.EntityChangeReport.ChangeInfo> changes)
    {
        AuditLogInfo audit;
        try
        {
            audit = Create<IAuditingManager>()?.Current?.Log;
            if (audit == null || changes == null)
                return;
            audit.EntityChanges ??= new List<EntityChangeInfo>();
        }
        catch (Exception)
        {
            // 仅隔离自动快照自身的采集；宿主原有回调仍由调用方正常执行。
            return;
        }
        foreach (var item in changes)
        {
            try
            {
                CaptureAuditChange(audit, item);
            }
            catch (Exception)
            {
                // 元数据、身份读取或合并失败时跳过本实体，不误报已经完成的业务保存。
            }
        }
    }

    /// <summary>
    /// 采集单个实体快照，跳过无法读取或格式化的属性。
    /// </summary>
    /// <param name="audit">当前活动审计日志。</param>
    /// <param name="item">FreeSQL 通知中的单个实体变化。</param>
    private void CaptureAuditChange(AuditLogInfo audit, DbContext.EntityChangeReport.ChangeInfo item)
    {
        if (item == null || item.Object == null || item.Type == DbContext.EntityChangeType.SqlRaw)
            return;
        var type = item.EntityType ?? item.Object.GetType();
        var table = Orm.CodeFirst.GetTableByEntity(type);
        if (table == null)
            return;
        var changeType = item.Type == DbContext.EntityChangeType.Insert
            ? AuditEntityChangeType.Created
            : item.Type == DbContext.EntityChangeType.Delete ? AuditEntityChangeType.Deleted : AuditEntityChangeType.Updated;
        var entityId = table.Primarys?.Length > 0
            ? string.Join(",", table.Primarys.Select(column =>
                FormatAuditValue(table.Properties[column.CsName].GetValue(item.Object))))
            : null;
        var tenantProperty = table.ColumnsByCs.Values.FirstOrDefault(column =>
            string.Equals(column.CsName, "TenantId", StringComparison.OrdinalIgnoreCase));
        var change = new EntityChangeInfo
        {
            ChangeTime = DateTime.UtcNow,
            ChangeType = changeType,
            EntityId = entityId,
            EntityTenantId = tenantProperty == null ? null :
                FormatAuditValue(table.Properties[tenantProperty.CsName].GetValue(item.Object)),
            EntityTypeFullName = type.FullName ?? type.Name,
            PropertyChanges = new List<EntityPropertyChangeInfo>()
        };
        foreach (var column in table.ColumnsByCs.Values)
        {
            try
            {
                if (column.Attribute?.IsIgnore == true || IsSensitiveAuditProperty(column.CsName) ||
                    !table.Properties.TryGetValue(column.CsName, out var property) || property.GetIndexParameters().Length != 0)
                    continue;
                var originalValue = changeType == AuditEntityChangeType.Created || item.BeforeObject == null
                    ? null
                    : property.GetValue(item.BeforeObject);
                if (changeType == AuditEntityChangeType.Deleted)
                    originalValue = property.GetValue(item.Object);
                var currentValue = changeType == AuditEntityChangeType.Deleted
                    ? null
                    : property.GetValue(item.Object);
                if (changeType == AuditEntityChangeType.Updated && AreAuditValuesEqual(originalValue, currentValue))
                    continue;
                change.PropertyChanges.Add(new EntityPropertyChangeInfo
                {
                    PropertyName = column.CsName,
                    PropertyTypeFullName = property.PropertyType.FullName,
                    OriginalValue = FormatAuditValue(originalValue),
                    NewValue = FormatAuditValue(currentValue)
                });
            }
            catch (Exception)
            {
                // 反射 getter 与值格式化可能执行实体代码；失败属性不进入快照。
            }
        }
        if (change.PropertyChanges.Count == 0)
            return;
        var existing = audit.EntityChanges.FirstOrDefault(candidate =>
            string.Equals(candidate.EntityTypeFullName, change.EntityTypeFullName, StringComparison.Ordinal) &&
            string.Equals(candidate.EntityId, change.EntityId, StringComparison.Ordinal));
        if (existing == null)
            audit.EntityChanges.Add(change);
        else
        {
            if (changeType == AuditEntityChangeType.Deleted)
                existing.ChangeType = AuditEntityChangeType.Deleted;
            existing.PropertyChanges ??= new List<EntityPropertyChangeInfo>();
            existing.Merge(change);
        }
    }

    /// <summary>
    /// 判断默认不允许进入审计快照的敏感属性名称。
    /// </summary>
    /// <param name="name">映射的 CLR 属性名称。</param>
    /// <returns>名称包含密码、令牌、秘密或凭据标记时返回 true，否则返回 false。</returns>
    private static bool IsSensitiveAuditProperty(string name) => !string.IsNullOrWhiteSpace(name) &&
        (name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
         name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
         name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
         name.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>
    /// 将属性值复制为与 EFCore 对称的有界审计文本。
    /// </summary>
    /// <param name="value">当前通知中的属性值。</param>
    /// <returns>空值返回 null；其他值使用固定区域格式并最多保留 512 个字符。</returns>
    private static string FormatAuditValue(object value)
    {
        if (value == null)
            return null;
        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        return text == null || text.Length <= 512 ? text : text.Substring(0, 512);
    }

    /// <summary>
    /// 比较未截断的审计属性值。
    /// </summary>
    /// <param name="original">原始值。</param>
    /// <param name="current">当前值。</param>
    /// <returns>值相同时返回 <see langword="true"/>。</returns>
    private static bool AreAuditValuesEqual(object original, object current)
    {
        if (ReferenceEquals(original, current))
            return true;
        if (original is byte[] originalBytes && current is byte[] currentBytes)
            return originalBytes.SequenceEqual(currentBytes);
        return Equals(original, current);
    }

    /// <summary>
    /// 保存当前上下文的实体更改。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响的实体行数。</returns>
    protected async Task<int> SaveChangesCoreAsync(CancellationToken cancellationToken)
    {
        var owner = UnitOfWork;
        UnitOfWork = new DeferredCommitUnitOfWork(owner);
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            UnitOfWork = owner;
        }
    }

    /// <summary>
    /// 保存成功后执行通知。
    /// </summary>
    private void NotifyAfterSave()
    {
        try
        {
            SaveChangesAfter().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new PostCommitNotificationException(exception,
                UnitOfWork.GetOrBeginTransaction(false) == null ? TransactionOutcome.Committed : TransactionOutcome.Unknown);
        }
    }

    /// <summary>
    /// 保存成功后异步执行通知。
    /// </summary>
    private async Task NotifyAfterSaveAsync()
    {
        try
        {
            await SaveChangesAfter();
        }
        catch (Exception exception)
        {
            throw new PostCommitNotificationException(exception,
                UnitOfWork.GetOrBeginTransaction(false) == null ? TransactionOutcome.Committed : TransactionOutcome.Unknown);
        }
    }

    /// <summary>
    /// 保存后执行事件通知。
    /// </summary>
    protected virtual Task SaveChangesAfter() => PublishEventsAsync();

    /// <summary>
    /// 发布提交后的事件。
    /// </summary>
    protected virtual Task PublishEventsAsync() => Task.CompletedTask;

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    /// <returns>当前事务使用的数据库连接；未创建事务时返回 <see langword="null"/>。</returns>
    public IDbConnection GetConnection() => base.UnitOfWork.GetOrBeginTransaction()?.Connection;

}
