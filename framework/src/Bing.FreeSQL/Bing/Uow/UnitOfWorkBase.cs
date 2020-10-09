using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Bing.Aspects;
using Bing.Auditing;
using Bing.Data.Sql;
using Bing.Data.Sql.Matedatas;
using Bing.Data.Transaction;
using Bing.DependencyInjection;
using Bing.Domain.Entities;
using Bing.Exceptions;
using Bing.FreeSQL;
using Bing.Helpers;
using Bing.Sessions;
using Bing.Users;
using FreeSql;
using FreeSql.Internal.CommonProvider;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Uow
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase : DbContext, Bing.Uow.IUnitOfWork, IDatabase, IEntityMatedata
    {
        #region 字段

        /// <summary>
        /// 映射字典
        /// </summary>
        private static readonly ConcurrentDictionary<Type, IEnumerable<IMap>> Maps;

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
        /// 服务提供程序
        /// </summary>
        [Autowired]
        public IServiceProvider ServiceProvider { get; set; }

        /// <summary>
        /// 服务提供程序锁
        /// </summary>
        protected readonly object ServiceProviderLock = new object();

        /// <summary>
        /// 懒加载获取请求服务
        /// </summary>
        /// <typeparam name="TService">服务类型</typeparam>
        /// <param name="reference">服务引用</param>
        protected TService LazyGetRequiredService<TService>(ref TService reference) => LazyGetRequiredService(typeof(TService), ref reference);

        /// <summary>
        /// 懒加载获取请求服务
        /// </summary>
        /// <typeparam name="TRef">引用类型</typeparam>
        /// <param name="serviceType">服务类型</param>
        /// <param name="reference">服务引用</param>
        protected TRef LazyGetRequiredService<TRef>(Type serviceType, ref TRef reference)
        {
            if (reference == null)
            {
                lock (ServiceProviderLock)
                {
                    if (reference == null)
                        reference = (TRef)ServiceProvider.GetRequiredService(serviceType);
                }
            }
            return reference;
        }

        /// <summary>
        /// 用户会话
        /// </summary>
        protected ISession Session => LazyGetRequiredService(ref _session);

        /// <summary>
        /// 用户会话
        /// </summary>
        private ISession _session;

        /// <summary>
        /// 当前用户
        /// </summary>
        protected ICurrentUser CurrentUser => LazyGetRequiredService(ref _currentUser);

        /// <summary>
        /// 当前用户
        /// </summary>
        private ICurrentUser _currentUser;

        #endregion

        #region 静态构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的静态实例
        /// </summary>
        static UnitOfWorkBase()
        {
            Maps = new ConcurrentDictionary<Type, IEnumerable<IMap>>();
        }

        #endregion

        #region 构造函数

        protected UnitOfWorkBase(IFreeSql orm,  IServiceProvider serviceProvider) : base(orm, null)
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

        #region OnConfiguring(配置)

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">配置生成器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            base.OnConfiguring(builder);
        }

        #endregion

        #region OnModelCreating(配置映射)

        /// <summary>
        /// 配置映射
        /// </summary>
        /// <param name="modelBuilder">映射生成器</param>
        protected override void OnModelCreating(ICodeFirst modelBuilder)
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
        protected virtual Assembly[] GetAssemblies() => new[] {GetType().Assembly};

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
            catch (Exception ex)
            {
                if (ex.Message.Contains("【行级乐观锁】"))
                    throw new ConcurrencyException(ex);
                throw;
            }
        }

        #endregion

        #region CommitAsync(异步提交)

        /// <summary>
        /// 提交，返回影响的行数
        /// </summary>
        public async Task<int> CommitAsync()
        {
            try
            {
                return await SaveChangesAsync();
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

        /// <summary>
        /// 保存更改
        /// </summary>
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
            foreach (var entry in UnitOfWork.EntityChangeReport.Report)
            {
                switch (entry.Type)
                {
                    case EntityChangeType.Insert:
                        InterceptAddedOperation(entry);
                        break;

                    case EntityChangeType.Update:
                        InterceptModifiedOperation(entry);
                        break;

                    case EntityChangeType.Delete:
                        InterceptDeletedOperation(entry);
                        break;
                }
            }
        }

        /// <summary>
        /// 拦截添加操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptAddedOperation(EntityChangeReport.ChangeInfo entry)
        {
            InitCreationAudited(entry);
            InitModificationAudited(entry);
        }

        /// <summary>
        /// 初始化创建审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        private void InitCreationAudited(EntityChangeReport.ChangeInfo entry) => CreationAuditedInitializer.Init(entry.Object, GetUserId(), GetUserName());

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

        /// <summary>
        /// 获取用户会话
        /// </summary>
        protected virtual ISession GetSession() => Session;

        /// <summary>
        /// 初始化修改审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        private void InitModificationAudited(EntityChangeReport.ChangeInfo entry) => ModificationAuditedInitializer.Init(entry.Object, GetUserId(), GetUserName());

        /// <summary>
        /// 拦截修改操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptModifiedOperation(EntityChangeReport.ChangeInfo entry) => InitModificationAudited(entry);

        /// <summary>
        /// 拦截删除操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptDeletedOperation(EntityChangeReport.ChangeInfo entry) => DeletionAuditedInitializer.Init(entry.Object, GetUserId(), GetUserName());

        #endregion

        #region SaveChangesAsync(异步保存更改)

        /// <summary>
        /// 异步保存更改
        /// </summary>
        public override async Task<int> SaveChangesAsync()
        {
            SaveChangesBefore();
            var transactionActionManager = Create<ITransactionActionManager>();
            if (transactionActionManager.Count == 0)
                return await base.SaveChangesAsync();
            return await TransactionCommit(transactionActionManager);
        }

        /// <summary>
        /// 手工创建事务提交
        /// </summary>
        /// <param name="transactionActionManager">事务操作管理器</param>
        private async Task<int> TransactionCommit(ITransactionActionManager transactionActionManager)
        {
            using var transaction = UnitOfWork.GetOrBeginTransaction();
            try
            {
                await transactionActionManager.CommitAsync(transaction);
                var result = await base.SaveChangesAsync();
                transaction.Commit();
                return result;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        #endregion

        #region InitVersion(初始化版本号)

        /// <summary>
        /// 初始化版本号
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected void InitVersion(EntityChangeReport.ChangeInfo entry)
        {
            if (!(entry.Object is IVersion entity))
                return;
            entity.Version = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
        }

        #endregion

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public IDbConnection GetConnection() => base.UnitOfWork.GetOrBeginTransaction()?.Connection;

        #region Matedata(获取元数据)

        /// <summary>
        /// 获取表名
        /// </summary>
        /// <param name="entity">实体类型</param>
        public virtual string GetTable(Type entity)
        {
            if (entity == null)
                return null;
            try
            {
                return Orm.CodeFirst.GetTableByEntity(entity)?.DbName;
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
        public virtual string GetSchema(Type entity)
        {
            if (entity == null)
                return null;
            try
            {
                var dbName = Orm.CodeFirst.GetTableByEntity(entity)?.DbName;
                var names = (Orm.Select<object>() as Select0Provider)._commonUtils.SplitTableName(dbName);
                return names.Length == 2 ? names[0] : null;
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
        public virtual string GetColumn(Type entity, string property)
        {
            if (entity == null || string.IsNullOrWhiteSpace(property))
                return null;
            try
            {
                var tableInfo = Orm.CodeFirst.GetTableByEntity(entity);
                var result = tableInfo?.ColumnsByCs[property]?.Attribute?.Name;
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
