using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Bing.Aspects;
using Bing.Auditing;
using Bing.Data;
using Bing.Data.Filters;
using Bing.Data.Sql.Metadata;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Domain.Entities.Events;
using Bing.EntityFrameworkCore.Modeling;
using Bing.Exceptions;
using Bing.Expressions;
using Bing.Extensions;
using Bing.Uow;
using Bing.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Core;

/// <summary>
/// 工作单元
/// </summary>
public abstract class UnitOfWorkBase : DbContext, IUnitOfWork, IDatabase
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
    /// 保存后需要刷新数据库生成主键的审计记录。
    /// </summary>
    private readonly List<(EntityEntry Entry, EntityChangeInfo Change)> _pendingCreatedAuditChanges = new();

    /// <summary>
    /// 缓存用于配置基础实体属性的反射方法信息。
    /// </summary>
    private static readonly MethodInfo _configureBasePropertiesMethodInfo = typeof(UnitOfWorkBase).GetMethod(nameof(ConfigureBaseProperties), BindingFlags.Instance | BindingFlags.NonPublic);

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
    /// <param name="options">配置</param>
    /// <param name="serviceProvider">服务提供器</param>
    protected UnitOfWorkBase(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options)
    {
        TraceId = Guid.NewGuid().ToString();
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Logger = serviceProvider.GetLogger(GetType());
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
    /// <returns>解析到的指定类型服务实例；未找到时返回默认值。</returns>
    private T Create<T>()
    {
        var result = _serviceProvider.GetService(typeof(T));
        if (result == null)
            return default;
        return (T)result;
    }

    /// <summary>
    /// 获取作用域服务提供程序
    /// </summary>
    internal IServiceProvider ServiceProvider => _serviceProvider;

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
    /// 获取审计属性设置器
    /// </summary>
    protected IAuditPropertySetter AuditPropertySetter => LazyServiceProvider.LazyGetRequiredService<IAuditPropertySetter>();

    /// <summary>
    /// 获取数据过滤管理器
    /// </summary>
    protected IFilterManager FilterManager => LazyServiceProvider.LazyGetRequiredService<IFilterManager>();

    /// <summary>
    /// 获取逻辑删除过滤器是否启用
    /// </summary>
    protected virtual bool IsSoftDeleteFilterEnabled => FilterManager?.IsEnabled<ISoftDelete>() ?? false;

    /// <summary>
    /// 获取日志对象
    /// </summary>
    protected ILogger Logger { get; }

    #endregion

    #region 辅助操作

    /// <summary>
    /// 获取用户标识
    /// </summary>
    /// <returns>当前用户标识。</returns>
    protected virtual string GetUserId() => CurrentUser.UserId;

    /// <summary>
    /// 获取用户名称
    /// </summary>
    /// <returns>当前用户名称；全名为空时返回用户名。</returns>
    protected virtual string GetUserName()
    {
        var name = CurrentUser.GetFullName();
        return string.IsNullOrEmpty(name) ? CurrentUser.GetUserName() : name;
    }

    #endregion

    #region OnConfiguring(配置)

    /// <inheritdoc />
    /// <remarks>
    /// 配置每次创建 DbContext 时执行的基础选项。
    ///
    /// 每次创建新的 DbContext 对象时都会调用。
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        ConfiguringLog(builder);
    }

    #endregion

    #region ConfiguringLog(配置日志)

    /// <summary>
    /// 配置日志
    /// </summary>
    /// <param name="builder">配置生成器</param>
    protected virtual void ConfiguringLog(DbContextOptionsBuilder builder)
    {
        ConfiguringIgnoreEvent(builder);
        builder.EnableDetailedErrors();
    }

    /// <summary>
    /// 配置忽略事件
    /// </summary>
    /// <param name="builder">配置事件</param>
    /// <remarks>参考：https://docs.microsoft.com/zh-cn/ef/core/logging-events-diagnostics </remarks>
    protected virtual void ConfiguringIgnoreEvent(DbContextOptionsBuilder builder)
    {
        builder.ConfigureWarnings(x => x.Ignore(
            RelationalEventId.ConnectionOpening,
            RelationalEventId.ConnectionOpened,
            RelationalEventId.DataReaderDisposing,
            RelationalEventId.ConnectionClosing,
            RelationalEventId.ConnectionClosed,
            CoreEventId.ServiceProviderCreated,
            CoreEventId.ServiceProviderDebugInfo,
            CoreEventId.ContextInitialized,
            CoreEventId.ContextDisposed,
            CoreEventId.QueryExecutionPlanned,
            CoreEventId.StartedTracking,
            CoreEventId.DetectChangesStarting,
            CoreEventId.DetectChangesCompleted,
            CoreEventId.SaveChangesStarting,
            CoreEventId.PropertyChangeDetected,
            RelationalEventId.TransactionStarted,
            RelationalEventId.TransactionDisposed
        ));
    }

    #endregion

    #region OnModelCreating(配置实体模型)

    /// <inheritdoc />
    /// <remarks>
    /// 只会调用一次，创建上下文数据模型时，对各个实体类的数据库映射细节进行配置
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyConfigurations(modelBuilder);
        var mappers = GetMaps();
        foreach (var mapper in mappers)
            mapper.Map(modelBuilder);
    }

    /// <summary>
    /// 获取映射配置列表
    /// </summary>
    /// <returns>当前工作单元使用的映射配置集合。</returns>
    private IEnumerable<IMap> GetMaps() => _maps.GetOrAdd(GetMapType(), GetMapsFromAssemblies());

    /// <summary>
    /// 获取映射接口类型
    /// </summary>
    /// <returns>当前工作单元对应的映射接口类型。</returns>
    protected virtual Type GetMapType() => this.GetType();

    /// <summary>
    /// 从程序集获取映射配置列表
    /// </summary>
    /// <returns>从相关程序集发现的映射配置集合。</returns>
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
    protected virtual Assembly[] GetAssemblies() => new[] { GetType().Assembly };

    #endregion

    #region ApplyConfigurations(配置实体类型)

    /// <summary>
    /// 配置实体类型
    /// </summary>
    /// <param name="modelBuilder">模型生成器</param>
    protected virtual void ApplyConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            _configureBasePropertiesMethodInfo
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, new object[] { modelBuilder, entityType });
        }
    }

    /// <summary>
    /// 配置基础属性
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="modelBuilder">模型生成器</param>
    /// <param name="entityType">实体类型</param>
    protected virtual void ConfigureBaseProperties<TEntity>(ModelBuilder modelBuilder, IMutableEntityType entityType) 
        where TEntity:class
    {
        if(entityType.IsOwned())
            return;
        modelBuilder.Entity<TEntity>().ConfigureByConvention();
        ConfigureGlobalFilters<TEntity>(modelBuilder, entityType);
    }

    #endregion

    #region ConfigureGlobalFilters(配置全局过滤器)

    /// <summary>
    /// 配置过滤器
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="modelBuilder">模型生成器</param>
    /// <param name="entityType">实体类型</param>
    protected virtual void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder, IMutableEntityType entityType) 
        where TEntity : class
    {
        if (FilterManager == null)
            return;
        if (entityType.BaseType == null && FilterManager.IsEntityEnabled<TEntity>())
        {
            var filterExpression = CreateFilterExpression<TEntity>();
            if (filterExpression != null)
                modelBuilder.Entity<TEntity>().HasQueryFilter(filterExpression);
        }
    }

    /// <summary>
    /// 创建过滤器表达式
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>适用于指定实体类型的全局过滤表达式；无需过滤时返回 null。</returns>
    protected virtual Expression<Func<TEntity, bool>> CreateFilterExpression<TEntity>() where TEntity : class
    {
        return GetSoftDeleteFilterExpression<TEntity>();
    }

    /// <summary>
    /// 获取逻辑删除过滤器表达式
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <returns>适用于指定实体类型的逻辑删除过滤表达式；过滤器不适用时返回 null。</returns>
    protected virtual Expression<Func<TEntity, bool>> GetSoftDeleteFilterExpression<TEntity>() where TEntity : class
    {
        var filter = FilterManager.GetFilter<ISoftDelete>();
        if (filter.IsEntityEnabled<TEntity>() == false)
            return null;
        var expression = filter.GetExpression<TEntity>();
        Expression<Func<TEntity, bool>> result = entity => !IsSoftDeleteFilterEnabled;
        return result.Or(expression);
    }

    #endregion

    #region Commit(提交)

    /// <inheritdoc />
    public int Commit()
    {
        try
        {
            return SaveChanges();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (ex.Entries.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ex.Entries.Count > 1
                    ? "There are some entries which are not saved due to concurrency exception:"
                    : "There is an entry which is not saved due to concurrency exception:");
                foreach (var entry in ex.Entries) 
                    sb.AppendLine(entry.ToString());
                Logger.LogWarning(sb.ToString());
            }
            throw new ConcurrencyException(ex.Message, ex);
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
        catch (DbUpdateConcurrencyException ex)
        {
            if (ex.Entries.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine(ex.Entries.Count > 1
                    ? "There are some entries which are not saved due to concurrency exception:"
                    : "There is an entry which is not saved due to concurrency exception:");
                foreach (var entry in ex.Entries) 
                    sb.AppendLine(entry.ToString());
                Logger.LogWarning(sb.ToString());
            }
            throw new ConcurrencyException(ex.Message, ex);
        }
    }

    #endregion

    #region SaveChanges(保存更改)

    /// <inheritdoc />
    public override int SaveChanges()
    {
        _pendingCreatedAuditChanges.Clear();
        ITransactionActionManager transactionActionManager;
        DomainEventDispatchSession domainEvents;
        try
        {
            SaveChangesBefore();
            transactionActionManager = Create<ITransactionActionManager>();
            domainEvents = PrepareDomainEvents(transactionActionManager);
        }
        catch
        {
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        var ownsCommit = Database.CurrentTransaction == null;
        int result;
        try
        {
            result = transactionActionManager.Count == 0
                ? base.SaveChanges()
                : TransactionCommit(transactionActionManager);
        }
        catch (Exception exception) when (exception.GetTransactionOutcome() == TransactionOutcome.Committed)
        {
            domainEvents?.CompleteAfterCommitFailure(exception);
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        catch
        {
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        try
        {
            RefreshGeneratedAuditEntityIds();
            domainEvents?.Complete();
            SaveChangesAfter().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _pendingCreatedAuditChanges.Clear();
            throw new PostCommitNotificationException(exception,
                ownsCommit ? TransactionOutcome.Committed : TransactionOutcome.Unknown);
        }
        _pendingCreatedAuditChanges.Clear();
        return result;
    }

    /// <summary>
    /// 手工创建事务提交。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器。</param>
    /// <returns>事务提交并保存更改影响的实体行数。</returns>
    protected virtual int TransactionCommit(ITransactionActionManager transactionActionManager)
    {
        var connection = Database.GetDbConnection();
        var openedHere = false;
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;
        var outcome = TransactionOutcome.Unknown;
        var result = 0;
        try
        {
            if (connection.State == ConnectionState.Closed)
            {
                connection.Open();
                openedHere = true;
            }
            using (var transaction = connection.BeginTransaction())
            {
                var shouldRollback = false;
                try
                {
                    Database.UseTransaction(transaction);
                    transactionActionManager.CommitAsync(transaction).GetAwaiter().GetResult();
                    result = base.SaveChanges();
                }
                catch (Exception exception)
                {
                    primaryException = exception;
                    shouldRollback = true;
                }
                if (shouldRollback)
                {
                    try
                    {
                        transaction.Rollback();
                        outcome = TransactionOutcome.RolledBack;
                    }
                    catch (Exception rollbackException)
                    {
                        cleanupExceptions.Add(rollbackException);
                    }
                }
                else
                {
                    try
                    {
                        transaction.Commit();
                        outcome = TransactionOutcome.Committed;
                    }
                    catch (Exception commitException)
                    {
                        primaryException = commitException;
                    }
                }
                try
                {
                    Database.UseTransaction(null);
                }
                catch (Exception detachException)
                {
                    cleanupExceptions.Add(detachException);
                }
            }
        }
        catch (Exception exception)
        {
            if (primaryException == null)
                primaryException = exception;
            else
                cleanupExceptions.Add(exception);
        }
        finally
        {
            if (openedHere)
            {
                try
                {
                    connection.Close();
                }
                catch (Exception closeException)
                {
                    cleanupExceptions.Add(closeException);
                }
            }
        }

        if (primaryException != null)
        {
            if (cleanupExceptions.Count > 0)
            {
                cleanupExceptions.Insert(0, primaryException);
                var failure = new AggregateException(cleanupExceptions);
                failure.SetTransactionOutcome(outcome);
                throw failure;
            }
            primaryException.SetTransactionOutcome(outcome);
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        }
        if (cleanupExceptions.Count == 1)
        {
            cleanupExceptions[0].SetTransactionOutcome(outcome);
            ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
        }
        if (cleanupExceptions.Count > 1)
        {
            var failure = new AggregateException(cleanupExceptions);
            failure.SetTransactionOutcome(outcome);
            throw failure;
        }
        return result;
    }

    #endregion

    #region SaveChangesAsync(保存)

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _pendingCreatedAuditChanges.Clear();
        ITransactionActionManager transactionActionManager;
        DomainEventDispatchSession domainEvents;
        try
        {
            SaveChangesBefore();
            transactionActionManager = Create<ITransactionActionManager>();
            domainEvents = PrepareDomainEvents(transactionActionManager);
        }
        catch
        {
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        var ownsCommit = Database.CurrentTransaction == null;
        int result;
        try
        {
            result = transactionActionManager.Count == 0
                ? await base.SaveChangesAsync(cancellationToken)
                : await TransactionCommitAsync(transactionActionManager, cancellationToken);
        }
        catch (Exception exception) when (exception.GetTransactionOutcome() == TransactionOutcome.Committed)
        {
            domainEvents?.CompleteAfterCommitFailure(exception);
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        catch
        {
            _pendingCreatedAuditChanges.Clear();
            throw;
        }
        try
        {
            RefreshGeneratedAuditEntityIds();
            domainEvents?.Complete();
            await SaveChangesAfter();
        }
        catch (Exception exception)
        {
            _pendingCreatedAuditChanges.Clear();
            throw new PostCommitNotificationException(exception,
                ownsCommit ? TransactionOutcome.Committed : TransactionOutcome.Unknown);
        }
        _pendingCreatedAuditChanges.Clear();
        return result;
    }

    /// <summary>
    /// 手工创建事务提交
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步事务提交操作的任务，结果为事务提交并保存更改影响的实体行数。</returns>
    protected virtual async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager, CancellationToken cancellationToken)
    {
        var connection = Database.GetDbConnection();
        var openedHere = false;
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;
        var outcome = TransactionOutcome.Unknown;
        var result = 0;
        try
        {
            if (connection.State == ConnectionState.Closed)
            {
                await connection.OpenAsync(cancellationToken);
                openedHere = true;
            }
            await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
            {
                var shouldRollback = false;
                try
                {
                    await Database.UseTransactionAsync(transaction, cancellationToken);
                    await transactionActionManager.CommitAsync(transaction);
                    cancellationToken.ThrowIfCancellationRequested();
                    result = await SaveChangesCoreAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    primaryException = exception;
                    shouldRollback = true;
                }
                if (shouldRollback)
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        outcome = TransactionOutcome.RolledBack;
                    }
                    catch (Exception rollbackException)
                    {
                        cleanupExceptions.Add(rollbackException);
                    }
                }
                else
                {
                    try
                    {
                        await transaction.CommitAsync(cancellationToken);
                        outcome = TransactionOutcome.Committed;
                    }
                    catch (Exception commitException)
                    {
                        primaryException = commitException;
                    }
                }
                try
                {
                    await Database.UseTransactionAsync(null, CancellationToken.None);
                }
                catch (Exception detachException)
                {
                    cleanupExceptions.Add(detachException);
                }
            }
        }
        catch (Exception exception)
        {
            if (primaryException == null)
                primaryException = exception;
            else
                cleanupExceptions.Add(exception);
        }
        finally
        {
            if (openedHere)
            {
                try
                {
                    await connection.CloseAsync();
                }
                catch (Exception closeException)
                {
                    cleanupExceptions.Add(closeException);
                }
            }
        }

        if (primaryException != null)
        {
            if (cleanupExceptions.Count > 0)
            {
                cleanupExceptions.Insert(0, primaryException);
                var failure = new AggregateException(cleanupExceptions);
                failure.SetTransactionOutcome(outcome);
                throw failure;
            }
            primaryException.SetTransactionOutcome(outcome);
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        }
        if (cleanupExceptions.Count == 1)
        {
            cleanupExceptions[0].SetTransactionOutcome(outcome);
            ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
        }
        if (cleanupExceptions.Count > 1)
        {
            var failure = new AggregateException(cleanupExceptions);
            failure.SetTransactionOutcome(outcome);
            throw failure;
        }
        return result;
    }

    #endregion

    /// <summary>
    /// 保存当前上下文的实体更改。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>受影响的实体行数。</returns>
    protected Task<int> SaveChangesCoreAsync(CancellationToken cancellationToken) =>
        base.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// 登记当前跟踪聚合根的事务内领域事件分发。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器。</param>
    /// <returns>需要在保存成功后确认的事件分发会话。</returns>
    private DomainEventDispatchSession PrepareDomainEvents(ITransactionActionManager transactionActionManager)
    {
        var dispatcher = Create<IDomainEventDispatcher>();
        if (dispatcher == null)
            return null;

        IEnumerable<IHasDomainEvents> GetSources() => ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>();

        if (!GetSources().Any(source => source.GetDomainEvents()?.Count > 0))
            return null;
        if (Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("外部事务中的领域事件无法确认最终提交，请由工作单元管理事务。");

        var session = new DomainEventDispatchSession(GetSources, dispatcher);
        transactionActionManager.Register(_ => session.DispatchAsync());
        return session;
    }

    #region SaveChangesBefore(保存前操作)

    /// <summary>
    /// 保存前操作
    /// </summary>
    protected virtual void SaveChangesBefore()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    ApplyInterceptForAddedEntity(entry);
                    break;

                case EntityState.Modified:
                    ApplyInterceptForModifiedEntity(entry);
                    break;

                case EntityState.Deleted:
                    ApplyInterceptForDeletedEntity(entry);
                    break;
            }
        }
    }

    #endregion

    #region ApplyInterceptForAddedEntity(添加前操作)

    /// <summary>
    /// 添加前操作
    /// </summary>
    /// <param name="entry">输入实体</param>
    protected virtual void ApplyInterceptForAddedEntity(EntityEntry entry)
    {
        CaptureAuditChanges(entry, EntityChangeType.Created);
        AuditPropertySetter?.SetCreationProperties(entry.Entity);
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
        SetVersion(entry);
    }

    #endregion

    #region ApplyInterceptForModifiedEntity(修改前操作)

    /// <summary>
    /// 修改前操作
    /// </summary>
    /// <param name="entry">输入实体</param>
    protected virtual void ApplyInterceptForModifiedEntity(EntityEntry entry)
    {
        CaptureAuditChanges(entry, EntityChangeType.Updated);
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
        SetVersion(entry);
    }

    #endregion

    #region InterceptDeletedOperation(删除前操作

    /// <summary>
    /// 删除前操作
    /// </summary>
    /// <param name="entry">输入实体</param>
    protected virtual void ApplyInterceptForDeletedEntity(EntityEntry entry)
    {
        CaptureAuditChanges(entry, EntityChangeType.Deleted);
        AuditPropertySetter?.SetDeletionProperties(entry.Entity);
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
        SetVersion(entry);
    }

    /// <summary>
    /// 将当前 EFCore 实体变化加入活动审计作用域。
    /// </summary>
    /// <param name="entry">待保存的实体条目。</param>
    /// <param name="changeType">实体变化类型。</param>
    /// <remarks>没有活动审计作用域时不执行任何捕获；敏感属性只在内存采集阶段直接排除。</remarks>
    private void CaptureAuditChanges(EntityEntry entry, EntityChangeType changeType)
    {
        var audit = Create<IAuditingManager>()?.Current?.Log;
        if (audit == null || entry?.Metadata?.ClrType == null)
            return;
        audit.EntityChanges ??= new List<EntityChangeInfo>();

        var entityId = GetAuditEntityId(entry);
        var tenantId = entry.Properties.FirstOrDefault(property =>
            string.Equals(property.Metadata.Name, "TenantId", StringComparison.OrdinalIgnoreCase));
        var tenantName = tenantId?.Metadata.Name;
        string formattedTenantId;
        if (!TryReadAuditValue(() => tenantId?.CurrentValue, entry.Metadata.ClrType, tenantName, out var tenantValue) ||
            !TryFormatAuditValue(tenantValue, entry.Metadata.ClrType, tenantName, out formattedTenantId))
            formattedTenantId = null;
        var change = new EntityChangeInfo
        {
            ChangeTime = DateTime.UtcNow,
            ChangeType = changeType,
            EntityId = entityId,
            EntityTenantId = formattedTenantId,
            EntityTypeFullName = entry.Metadata.ClrType.FullName ?? entry.Metadata.ClrType.Name,
            PropertyChanges = new List<EntityPropertyChangeInfo>()
        };

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (property.Metadata.IsShadowProperty() || IsSensitiveAuditProperty(name))
                continue;
            if (changeType == EntityChangeType.Updated && !property.IsModified)
                continue;
            if (!TryReadAuditValue(() => changeType == EntityChangeType.Created ? null : property.OriginalValue,
                    entry.Metadata.ClrType, name, out var originalValue) ||
                !TryReadAuditValue(() => changeType == EntityChangeType.Deleted ? null : property.CurrentValue,
                    entry.Metadata.ClrType, name, out var currentValue))
                continue;
            if (changeType == EntityChangeType.Updated && AreAuditValuesEqual(originalValue, currentValue))
                continue;
            if (!TryFormatAuditValue(originalValue, entry.Metadata.ClrType, name, out var original) ||
                !TryFormatAuditValue(currentValue, entry.Metadata.ClrType, name, out var current))
                continue;
            change.PropertyChanges.Add(new EntityPropertyChangeInfo
            {
                PropertyName = name,
                PropertyTypeFullName = property.Metadata.ClrType?.FullName,
                OriginalValue = original,
                NewValue = current
            });
        }

        if (change.PropertyChanges.Count == 0)
            return;
        var existing = changeType == EntityChangeType.Created
            ? null
            : audit.EntityChanges.FirstOrDefault(item =>
                string.Equals(item.EntityTypeFullName, change.EntityTypeFullName, StringComparison.Ordinal) &&
                string.Equals(item.EntityId, change.EntityId, StringComparison.Ordinal));
        if (existing == null)
            audit.EntityChanges.Add(change);
        else
        {
            if (changeType == EntityChangeType.Deleted)
                existing.ChangeType = EntityChangeType.Deleted;
            existing.PropertyChanges ??= new List<EntityPropertyChangeInfo>();
            existing.Merge(change);
            change = existing;
        }
        if (changeType == EntityChangeType.Created)
            _pendingCreatedAuditChanges.Add((entry, change));
    }

    /// <summary>
    /// 获取实体当前主键的审计文本。
    /// </summary>
    /// <param name="entry">实体条目。</param>
    /// <returns>由主键属性组成的标识文本。</returns>
    private string GetAuditEntityId(EntityEntry entry)
    {
        var primaryKey = entry?.Metadata?.FindPrimaryKey()?.Properties;
        if (primaryKey == null)
            return null;
        var values = new List<string>(primaryKey.Count);
        foreach (var property in primaryKey)
        {
            if (!TryReadAuditValue(() => entry.Property(property.Name).CurrentValue, entry.Metadata.ClrType,
                    property.Name, out var rawValue) ||
                !TryFormatAuditValue(rawValue, entry.Metadata.ClrType, property.Name, out var value))
                return null;
            values.Add(value);
        }
        return string.Join(",", values);
    }

    /// <summary>
    /// 将数据库生成的主键刷新到已采集的创建审计记录。
    /// </summary>
    private void RefreshGeneratedAuditEntityIds()
    {
        foreach (var pending in _pendingCreatedAuditChanges)
            pending.Change.EntityId = GetAuditEntityId(pending.Entry);
    }

    /// <summary>
    /// 判断属性名称是否属于默认敏感审计字段。
    /// </summary>
    private static bool IsSensitiveAuditProperty(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return name.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("credential", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 将属性值转换为有界的安全文本。
    /// </summary>
    private static string FormatAuditValue(object value)
    {
        if (value == null)
            return null;
        var text = value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
        if (text == null || text.Length <= 512)
            return text;
        return text.Substring(0, 512);
    }

    /// <summary>
    /// 尝试格式化审计值。
    /// </summary>
    /// <param name="value">待格式化的值。</param>
    /// <param name="entityType">所属实体类型。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="formattedValue">格式化后的值。</param>
    /// <returns>格式化成功时返回 <see langword="true"/>。</returns>
    /// <remarks>值对象格式化失败时跳过该值，不阻断业务保存；数据库和审计存储异常仍会继续传播。</remarks>
    private bool TryFormatAuditValue(object value, Type entityType, string propertyName, out string formattedValue)
    {
        try
        {
            formattedValue = FormatAuditValue(value);
            return true;
        }
        catch (Exception exception)
        {
            formattedValue = null;
            Logger.LogWarning("实体 {EntityType} 的审计属性 {PropertyName} 格式化失败，已跳过该审计值。异常类型：{ExceptionType}。",
                entityType?.FullName ?? entityType?.Name, propertyName, exception.GetType().FullName);
            return false;
        }
    }

    /// <summary>
    /// 尝试读取审计值。
    /// </summary>
    /// <param name="valueFactory">值读取委托。</param>
    /// <param name="entityType">所属实体类型。</param>
    /// <param name="propertyName">属性名称。</param>
    /// <param name="value">读取到的值。</param>
    /// <returns>读取成功时返回 <see langword="true"/>。</returns>
    private bool TryReadAuditValue(Func<object> valueFactory, Type entityType, string propertyName, out object value)
    {
        try
        {
            value = valueFactory();
            return true;
        }
        catch (Exception exception)
        {
            value = null;
            Logger.LogWarning("实体 {EntityType} 的审计属性 {PropertyName} 读取失败，已跳过该审计值。异常类型：{ExceptionType}。",
                entityType?.FullName ?? entityType?.Name, propertyName, exception.GetType().FullName);
            return false;
        }
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

    #endregion

    #region SetVersion(设置版本号)

    /// <summary>
    /// 设置版本号
    /// </summary>
    /// <param name="entry">输入实体</param>
    protected virtual void SetVersion(EntityEntry entry)
    {
        if (!(entry.Entity is IVersion entity))
            return;
        var version = GetVersion();
        if (version == null)
            return;
        entity.Version = version;
    }

    #endregion

    #region GetVersion(获取版本号)

    /// <summary>
    /// 获取版本号
    /// </summary>
    /// <returns>新生成的实体版本号。</returns>
    protected virtual byte[] GetVersion() => Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());

    #endregion

    #region GetConnection(获取数据库连接)

    /// <inheritdoc />
    public IDbConnection GetConnection() => Database.GetDbConnection();

    #endregion

    #region SaveChangeAfter(保存后操作)

    /// <summary>
    /// 保存后操作
    /// </summary>
    protected virtual async Task SaveChangesAfter()
    {
        await PublishEventsAsync();
    }

    #endregion

    #region PublishEventsAsync(发布事件)

    /// <summary>
    /// 发布事件
    /// </summary>
    protected virtual Task PublishEventsAsync()
    {
        return Task.CompletedTask;
    }

    #endregion
}
