using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Logs;
using Bing.Datas.Sql;
using Bing.Datas.Sql.Matedatas;
using Bing.Datas.Transactions;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Exceptions;
using Bing.Helpers;
using Bing.Logs;
using Bing.Security.Extensions;
using Bing.Sessions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase:DbContext,IUnitOfWork,IDatabase,IEntityMatedata
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
        private IServiceProvider _serviceProvider;

        #endregion

        #region 属性

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }

        #endregion

        #region 静态构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的静态实例
        /// </summary>
        static UnitOfWorkBase()
        {
            Maps = new ConcurrentDictionary<Type, IEnumerable<IMap>>();
            LoggerFactory = new LoggerFactory(new[] {new EfLogProvider(),});
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="serviceProvider">服务提供器</param>
        protected UnitOfWorkBase(DbContextOptions options,IServiceProvider serviceProvider):base(options)
        {
            TraceId = Guid.NewGuid().ToString();
            Session = Bing.Sessions.Session.Instance;
            _serviceProvider = serviceProvider ?? Ioc.Create<IServiceProvider>();
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
        /// <returns></returns>
        private T Create<T>()
        {
            var result = _serviceProvider.GetService(typeof(T));
            if (result == null)
            {
                return default(T);
            }

            return (T)result;
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
        /// <param name="builder"></param>
        protected void EnableLog(DbContextOptionsBuilder builder)
        {
            var log = GetLog();
            if (IsEnabled(log) == false)
            {
                return;
            }
            builder.EnableSensitiveDataLogging();
            builder.EnableDetailedErrors();
            builder.UseLoggerFactory(LoggerFactory);
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        protected virtual ILog GetLog()
        {
            try
            {
                return Log.GetLog(EfLog.TraceLogName);
            }
            catch
            {
                return Log.Null;
            }
        }

        /// <summary>
        /// 是否启用EF日志
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <returns></returns>
        private bool IsEnabled(ILog log)
        {
            var config = GetConfig();
            if (config.LogLevel == DataLogLevel.Off)
            {
                return false;
            }

            if (log.IsTraceEnabled == false)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取配置
        /// </summary>
        /// <returns></returns>
        private DataConfig GetConfig()
        {
            try
            {
                var options = Create<IOptionsSnapshot<DataConfig>>();
                return options.Value;
            }
            catch
            {
                return new DataConfig() { LogLevel = DataLogLevel.Sql };
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
            foreach (var mapper in GetMaps())
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
            {
                result.AddRange(GetMapInstances(assembly));
            }

            return result;
        }

        /// <summary>
        /// 获取映射实例列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        protected virtual IEnumerable<IMap> GetMapInstances(Assembly assembly)
        {
            return Reflection.GetInstancesByInterface<IMap>(assembly);
        }

        /// <summary>
        /// 获取定义映射配置的程序集列表
        /// </summary>
        protected virtual Assembly[] GetAssemblies()
        {
            return new[] { GetType().Assembly };
        }        

        #endregion

        #region Commit(提交)
        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        /// <returns></returns>
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
        /// <returns></returns>
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
        /// <returns></returns>
        public override int SaveChanges()
        {
            SaveChangesBefore();
            return base.SaveChanges();
        }

        /// <summary>
        /// 保存更改前操作
        /// </summary>
        protected virtual void SaveChangesBefore()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        InterceptAddedOperation(entry);
                        break;
                    case EntityState.Modified:
                        InterceptModifiedOperation(entry);
                        break;
                    case EntityState.Deleted:
                        InterceptDeletedOperation(entry);
                        break;
                }
            }
        }

        /// <summary>
        /// 拦截添加操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptAddedOperation(EntityEntry entry)
        {
            InitCreationAudited(entry);
            InitModificationAudited(entry);
        }

        /// <summary>
        /// 初始化创建审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        private void InitCreationAudited(EntityEntry entry)
        {
            CreationAuditedInitializer.Init(entry.Entity, GetUserId(), GetUserName());
        }

        /// <summary>
        /// 获取用户标识
        /// </summary>
        /// <returns></returns>
        protected virtual string GetUserId() => GetSession().UserId;

        /// <summary>
        /// 获取用户名称
        /// </summary>
        /// <returns></returns>
        protected virtual string GetUserName()
        {
            var name = GetSession().GetFullName();
            return string.IsNullOrEmpty(name) ? GetSession().GetUserName() : name;
        }

        /// <summary>
        /// 获取用户会话
        /// </summary>
        /// <returns></returns>
        protected virtual ISession GetSession() => Session;

        /// <summary>
        /// 初始化修改审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        private void InitModificationAudited(EntityEntry entry)
        {
            ModificationAuditedInitializer.Init(entry.Entity, GetUserId(), GetUserName());
        }

        /// <summary>
        /// 拦截修改操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptModifiedOperation(EntityEntry entry) => InitModificationAudited(entry);

        /// <summary>
        /// 拦截删除操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptDeletedOperation(EntityEntry entry)
        {
            DeletionAuditedInitializer.Init(entry.Entity, GetUserId(), GetUserName());
        }

        #endregion

        #region SaveChangesAsync(异步保存更改)

        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            SaveChangesBefore();
            var transactionActionManager = Create<ITransactionActionManager>();
            if (transactionActionManager.Count == 0)
            {
                return await base.SaveChangesAsync(cancellationToken);
            }

            return await TransactionCommit(transactionActionManager, cancellationToken);
        }

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager,
            CancellationToken cancellationToken)
        {
            using (var connection = Database.GetDbConnection())
            {
                if (connection.State == ConnectionState.Closed)
                {
                    await connection.OpenAsync(cancellationToken);
                }

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        await transactionActionManager.CommitAsync(transaction);
                        Database.UseTransaction(transaction);
                        var result = await base.SaveChangesAsync(cancellationToken);
                        transaction.Commit();
                        return result;
                    }
                    catch (Exception e)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion

        #region InitVersion(初始化版本号)

        /// <summary>
        /// 初始化版本号
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected void InitVersion(EntityEntry entry)
        {
            if (!(entry.Entity is IVersion entity))
            {
                return;
            }
            entity.Version = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        }

        #endregion

        #region GetConnection(获取数据库连接)

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        /// <returns></returns>
        public IDbConnection GetConnection()
        {
            return Database.GetDbConnection();
        }

        #endregion

        #region Matedata(获取元数据)

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="entity">实体类型</param>
        /// <returns></returns>
        public string GetTable(Type entity)
        {
            if (entity == null)
            {
                return null;
            }

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
        /// <returns></returns>
        public string GetSchema(Type entity)
        {
            if (entity == null)
            {
                return null;
            }

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
        /// <returns></returns>
        public string GetColumn(Type entity, string property)
        {
            if (entity == null || string.IsNullOrWhiteSpace(property))
            {
                return null;
            }

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

    }
}
