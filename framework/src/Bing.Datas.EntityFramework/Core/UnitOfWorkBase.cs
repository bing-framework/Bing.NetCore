using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Aspects;
using Bing.Auditing;
using Bing.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Matedatas;
using Bing.Data.Transaction;
using Bing.Datas.EntityFramework.Logs;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Domain.Entities.Events;
using Bing.Exceptions;
using Bing.Extensions;
using Bing.Logs;
using Bing.Logs.Core;
using Bing.Uow;
using Bing.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase : DbContext, IUnitOfWork, IDatabase, IEntityMatedata
    {
        #region 字段

        /// <summary>
        /// 映射字典
        /// </summary>
        private static readonly ConcurrentDictionary<Type, IEnumerable<IMap>> Maps;

        /// <summary>
        /// 日志工厂
        /// </summary>
        private static readonly ILoggerFactory LoggerFactory;

        /// <summary>
        /// 服务提供器
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

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
        /// 日志
        /// </summary>
        public ILog Log => LazyServiceProvider.LazyGetService<ILog>() ?? NullLog.Instance;

        /// <summary>
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser => LazyServiceProvider.LazyGetRequiredService<ICurrentUser>();

        /// <summary>
        /// 审计属性设置器
        /// </summary>
        protected IAuditPropertySetter AuditPropertySetter => LazyServiceProvider.LazyGetRequiredService<IAuditPropertySetter>();

        #endregion

        #region 静态构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的静态实例
        /// </summary>
        static UnitOfWorkBase()
        {
            Maps = new ConcurrentDictionary<Type, IEnumerable<IMap>>();
            LoggerFactory = new LoggerFactory(new[] { new EfLogProvider(), });
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="serviceProvider">服务提供器</param>
        protected UnitOfWorkBase(DbContextOptions options, IServiceProvider serviceProvider) : base(options)
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
        private T Create<T>()
        {
            var result = _serviceProvider.GetService(typeof(T));
            if (result == null)
                return default;
            return (T)result;
        }

        #endregion

        #region 辅助操作

        /// <summary>
        /// 获取用户标识
        /// </summary>
        protected virtual string GetUserId() => CurrentUser.UserId;

        /// <summary>
        /// 获取用户名称
        /// </summary>
        protected virtual string GetUserName()
        {
            var name = CurrentUser.GetFullName();
            return string.IsNullOrEmpty(name) ? CurrentUser.GetUserName() : name;
        }

        #endregion

        #region OnConfiguring(配置)

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">配置生成器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder builder) => EnableLog(builder);

        /// <summary>
        /// 启用日志
        /// </summary>
        /// <param name="builder">配置生成器</param>
        protected void EnableLog(DbContextOptionsBuilder builder)
        {
            ConfiguringIgnoreEvent(builder);
            var log = Log;
            if (IsEnabled(log) == false)
                return;
            builder.EnableSensitiveDataLogging();
            builder.EnableDetailedErrors();
            //builder.UseLoggerFactory(LoggerFactory);
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
                CoreEventId.SensitiveDataLoggingEnabledWarning,
                CoreEventId.ContextInitialized,
                CoreEventId.ContextDisposed,
                CoreEventId.QueryModelCompiling,
                CoreEventId.QueryModelOptimized,
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

        /// <summary>
        /// 是否启用EF日志
        /// </summary>
        /// <param name="log">日志操作</param>
        private bool IsEnabled(ILog log)
        {
            var config = GetConfig();
            if (config.LogLevel == DataLogLevel.Off)
                return false;
            if (log.IsTraceEnabled == false)
                return false;
            return true;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        private DataConfig GetConfig()
        {
            try
            {
                var options = Create<IOptionsSnapshot<DataConfig>>();
                return options.Value;
            }
            catch
            {
                return new DataConfig { LogLevel = DataLogLevel.Sql };
            }
        }

        #endregion

        #region OnModelCreating(配置映射)

        /// <summary>
        /// 配置映射
        /// </summary>
        /// <param name="modelBuilder">映射生成器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var mappers = GetMaps();
            foreach (var mapper in mappers)
                mapper.Map(modelBuilder);
        }

        /// <summary>
        /// 获取映射配置列表
        /// </summary>
        private IEnumerable<IMap> GetMaps() => Maps.GetOrAdd(GetMapType(), GetMapsFromAssemblies());

        /// <summary>
        /// 获取映射接口类型
        /// </summary>
        protected virtual Type GetMapType() => this.GetType();

        /// <summary>
        /// 从程序集获取映射配置列表
        /// </summary>
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
        protected virtual IEnumerable<IMap> GetMapInstances(Assembly assembly) => Reflection.Reflections.GetInstancesByInterface<IMap>(assembly);

        /// <summary>
        /// 获取定义映射配置的程序集列表
        /// </summary>
        protected virtual Assembly[] GetAssemblies() => new[] { GetType().Assembly };

        #endregion

        #region Commit(提交)

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        public int Commit()
        {
            try
            {
                return SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException(ex);
            }
        }

        #endregion

        #region CommitAsync(异步提交)

        /// <summary>
        /// 异步提交，返回影响的行数
        /// </summary>
        public async Task<int> CommitAsync()
        {
            try
            {
                return await SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyException(ex);
            }
        }

        #endregion

        #region SaveChanges(保存更改)

        /// <summary>
        /// 保存更改
        /// </summary>
        public override int SaveChanges()
        {
            SaveChangesBefore();
            return base.SaveChanges();
        }

        #endregion

        #region SaveChangesAsync(保存)

        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await DomainEventHandleAsync();
            SaveChangesBefore();
            var transactionActionManager = Create<ITransactionActionManager>();
            if (transactionActionManager.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);
            return await TransactionCommit(transactionActionManager, cancellationToken);
        }

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            using var connection = Database.GetDbConnection();
            if (connection.State == ConnectionState.Closed)
                await connection.OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            try
            {
                await transactionActionManager.CommitAsync(transaction);
                await Database.UseTransactionAsync(transaction,cancellationToken);
                var result = await base.SaveChangesAsync(cancellationToken);
                transaction.Commit();
                return result;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 领域事件处理
        /// </summary>
        protected virtual async Task DomainEventHandleAsync()
        {
            var dispatcher = Create<IDomainEventDispatcher>();
            if (dispatcher != null)
            {
                var domainEvents = GetDomainEvents();
                foreach (var @event in domainEvents) 
                    await dispatcher.DispatchAsync(@event);
            }
        }

        /// <summary>
        /// 获取领域事件集合
        /// </summary>
        private IEnumerable<DomainEvent> GetDomainEvents()
        {
            var domainEvents = new List<DomainEvent>();
            foreach (var aggregateRoot in ChangeTracker.Entries<IAggregateRoot>())
            {
                var events = aggregateRoot.Entity.GetDomainEvents();
                if (events != null && events.Any())
                {
                    domainEvents.AddRange(events);
                    aggregateRoot.Entity.ClearDomainEvents();
                }
            }
            return domainEvents;
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
            AuditPropertySetter?.SetCreationProperties(entry);
            AuditPropertySetter?.SetModificationProperties(entry);
            //SetCreationAudited(entry);
            //SetModificationAudited(entry);
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
            AuditPropertySetter?.SetModificationProperties(entry);
            //SetModificationAudited(entry);
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
            AuditPropertySetter?.SetDeletionProperties(entry);
            AuditPropertySetter?.SetModificationProperties(entry);
            SetVersion(entry);
            //SetModificationAudited(entry);
        }

        #endregion

        #region SetCreationAudited(设置创建审计信息)

        /// <summary>
        /// 设置创建审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void SetCreationAudited(EntityEntry entry)
        {
            CreationAuditedInitializer.Init(entry.Entity, GetUserId(), GetUserName());
        }

        #endregion

        #region SetModificationAudited(设置修改审计信息)

        /// <summary>
        /// 设置修改审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void SetModificationAudited(EntityEntry entry)
        {
            ModificationAuditedInitializer.Init(entry.Entity, GetUserId(), GetUserName());
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
        protected virtual byte[] GetVersion() => Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());

        #endregion

        #region GetConnection(获取数据库连接)

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public IDbConnection GetConnection() => Database.GetDbConnection();

        #endregion

        #region Matedata(获取元数据)

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="entity">实体类型</param>
        public string GetTable(Type entity)
        {
            if (entity == null)
                return null;
            try
            {
                var entityType = Model.FindEntityType(entity);
                return entityType?.FindAnnotation("Relational:TableName")?.Value.SafeString();
            }
            catch
            {
                return entity.Name;
            }
        }

        /// <summary>
        /// 获取架构
        /// </summary>
        /// <param name="entity">实体类型</param>
        public string GetSchema(Type entity)
        {
            if (entity == null)
                return null;
            try
            {
                var entityType = Model.FindEntityType(entity);
                return entityType?.FindAnnotation("Relational:Schema")?.Value.SafeString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取列名
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <param name="property">属性名</param>
        public string GetColumn(Type entity, string property)
        {
            if (entity == null || string.IsNullOrWhiteSpace(property))
                return null;
            try
            {
                var entityType = Model.FindEntityType(entity);
                var result = entityType?.GetProperty(property)?.FindAnnotation("Relational:ColumnName")?.Value.SafeString();
                return string.IsNullOrWhiteSpace(result) ? property : result;
            }
            catch
            {
                return property;
            }
        }

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
        /// <returns></returns>
        protected virtual Task PublishEventsAsync()
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
