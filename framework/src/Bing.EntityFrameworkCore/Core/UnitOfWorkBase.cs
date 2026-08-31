using System.Collections.Concurrent;
using System.Data;
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
    /// 初始化一个<see cref="UnitOfWorkBase"/>类型的实例
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
    /// 跟踪号
    /// </summary>
    public string TraceId { get; set; }

    /// <summary>
    /// Lazy延迟加载服务提供程序
    /// </summary>
    [Autowired]
    public virtual ILazyServiceProvider LazyServiceProvider { get; set; }

    /// <summary>
    /// 当前用户
    /// </summary>
    protected ICurrentUser CurrentUser => LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();

    /// <summary>
    /// 审计属性设置器
    /// </summary>
    protected IAuditPropertySetter AuditPropertySetter => LazyServiceProvider.LazyGetRequiredService<IAuditPropertySetter>();

    /// <summary>
    /// 数据过滤管理器
    /// </summary>
    protected IFilterManager FilterManager => LazyServiceProvider.LazyGetRequiredService<IFilterManager>();

    /// <summary>
    /// 逻辑删除过滤器是否启用
    /// </summary>
    protected virtual bool IsSoftDeleteFilterEnabled => FilterManager?.IsEnabled<ISoftDelete>() ?? false;

    /// <summary>
    /// 日志对象
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

    /// <summary>
    /// 配置每次创建 DbContext 时执行的基础选项。
    /// </summary>
    /// <param name="builder">配置生成器</param>
    /// <remarks>每次创建新的 DbContext 对象时都会调用。</remarks>
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

    /// <summary>
    /// 配置实体模型
    /// </summary>
    /// <remarks>只会调用一次，创建上下文数据模型时，对各个实体类的数据库映射细节进行配置</remarks>
    /// <param name="modelBuilder">映射生成器</param>
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

    /// <summary>
    /// 提交，返回影响的行数
    /// </summary>
    /// <returns>本次提交影响的实体行数。</returns>
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

    /// <summary>
    /// 异步提交，返回影响的行数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步提交操作的任务，结果为本次提交影响的实体行数。</returns>
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

    /// <summary>
    /// 保存更改
    /// </summary>
    /// <returns>保存更改影响的实体行数。</returns>
    public override int SaveChanges()
    {
        SaveChangesBefore();
        var transactionActionManager = Create<ITransactionActionManager>();
        var result = transactionActionManager.Count == 0
            ? base.SaveChanges()
            : TransactionCommit(transactionActionManager);
        SaveChangesAfter().GetAwaiter().GetResult();
        return result;
    }

    /// <summary>
    /// 手工创建事务提交。
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器。</param>
    /// <returns>事务提交并保存更改影响的实体行数。</returns>
    private int TransactionCommit(ITransactionActionManager transactionActionManager)
    {
        var connection = Database.GetDbConnection();
        var openedHere = false;
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;
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
                    transactionActionManager.CommitAsync(transaction).GetAwaiter().GetResult();
                    Database.UseTransaction(transaction);
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
                throw new AggregateException(cleanupExceptions);
            }
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        }
        if (cleanupExceptions.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
        if (cleanupExceptions.Count > 1)
            throw new AggregateException(cleanupExceptions);
        return result;
    }

    #endregion

    #region SaveChangesAsync(保存)

    /// <summary>
    /// 异步保存更改
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步保存操作的任务，结果为保存更改影响的实体行数。</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveChangesBefore();
        var transactionActionManager = Create<ITransactionActionManager>();
        var result = transactionActionManager.Count == 0
            ? await base.SaveChangesAsync(cancellationToken)
            : await TransactionCommitAsync(transactionActionManager, cancellationToken);
        await SaveChangesAfter();
        return result;
    }

    /// <summary>
    /// 手工创建事务提交
    /// </summary>
    /// <param name="transactionActionManager">事务操作管理器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步事务提交操作的任务，结果为事务提交并保存更改影响的实体行数。</returns>
    private async Task<int> TransactionCommitAsync(ITransactionActionManager transactionActionManager, CancellationToken cancellationToken)
    {
        var connection = Database.GetDbConnection();
        var openedHere = false;
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;
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
                    await transactionActionManager.CommitAsync(transaction);
                    cancellationToken.ThrowIfCancellationRequested();
                    await Database.UseTransactionAsync(transaction, cancellationToken);
                    result = await base.SaveChangesAsync(cancellationToken);
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
                throw new AggregateException(cleanupExceptions);
            }
            ExceptionDispatchInfo.Capture(primaryException).Throw();
        }
        if (cleanupExceptions.Count == 1)
            ExceptionDispatchInfo.Capture(cleanupExceptions[0]).Throw();
        if (cleanupExceptions.Count > 1)
            throw new AggregateException(cleanupExceptions);
        return result;
    }

    #endregion

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
        AuditPropertySetter?.SetDeletionProperties(entry.Entity);
        AuditPropertySetter?.SetModificationProperties(entry.Entity);
        SetVersion(entry);
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

    /// <summary>
    /// 获取数据库连接
    /// </summary>
    /// <returns>当前工作单元使用的数据库连接。</returns>
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
