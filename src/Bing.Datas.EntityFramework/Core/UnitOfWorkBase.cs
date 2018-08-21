using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Logs;
using Bing.Datas.Matedatas;
using Bing.Datas.Sql;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Exceptions;
using Bing.Logs;
using Bing.Sessions;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase:DbContext,IUnitOfWork,IDatabase,IEntityMatedata
    {
        #region 属性

        /// <summary>
        /// 跟踪号
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// 用户会话
        /// </summary>
        public ISession Session { get; set; }

        /// <summary>
        /// 是否启用逻辑删除过滤
        /// </summary>
        protected virtual bool IsDeleteFilterEnabled => DataConfig.EnabledDeleteFilter;

        #endregion

        #region 字段

        private static MethodInfo ConfigureGlobalFiltersMethodInfo =
            typeof(UnitOfWorkBase).GetMethod(nameof(ConfigureGlobalFilters), BindingFlags.Instance | BindingFlags.NonPublic);

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="UnitOfWorkBase"/>类型的实例
        /// </summary>
        /// <param name="options">配置</param>
        /// <param name="manager">工作单元管理器</param>
        protected UnitOfWorkBase(DbContextOptions options, IUnitOfWorkManager manager):base(options)
        {
            manager?.Register(this);
            TraceId = Guid.NewGuid().ToString();
            Session = NullSession.Instance;
        }

        #endregion

        #region OnConfiguring(配置)

        /// <summary>
        /// 配置
        /// </summary>
        /// <param name="builder">配置生成器</param>
        protected override void OnConfiguring(DbContextOptionsBuilder builder)
        {
            EnableLog(builder);
        }

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
            builder.UseLoggerFactory(new LoggerFactory(new[] { GetLogProvider(log) }));
        }

        /// <summary>
        /// 获取日志操作
        /// </summary>
        /// <returns></returns>
        protected virtual ILog GetLog()
        {
            try
            {
                return Log.GetLogByName(EfLog.TRACE_LOG_NAME);
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
            return DataConfig.LogLevel != DataLogLevel.Off && log.IsTraceEnabled;
        }

        /// <summary>
        /// 获取日志提供器
        /// </summary>
        /// <param name="log">日志操作</param>
        /// <returns></returns>
        protected virtual ILoggerProvider GetLogProvider(ILog log)
        {
            return new EfLogProvider(log, this);
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

        #region OnModelCreating(配置映射)
        /// <summary>
        /// 配置映射
        /// </summary>
        /// <param name="modelBuilder">映射生成器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var mapper in GetMaps())
            {
                mapper.Map(modelBuilder);
            }

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                ConfigureGlobalFiltersMethodInfo.MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] {modelBuilder, entityType});
            }
        }

        /// <summary>
        /// 获取映射配置列表
        /// </summary>
        /// <returns></returns>
        private IEnumerable<IMap> GetMaps()
        {
            var result = new List<IMap>();
            foreach (var assembly in GetAssemblies())
            {
                result.AddRange(GetMapTypes(assembly));
            }
            return result;
        }

        /// <summary>
        /// 获取定义映射配置的程序集列表
        /// </summary>
        /// <returns></returns>
        protected virtual Assembly[] GetAssemblies()
        {
            return new[] { GetType().GetTypeInfo().Assembly };
        }

        /// <summary>
        /// 获取映射类型列表
        /// </summary>
        /// <param name="assembly">程序集</param>
        /// <returns></returns>
        protected virtual IEnumerable<IMap> GetMapTypes(Assembly assembly)
        {
            return Bing.Utils.Helpers.Reflection.GetInstancesByInterface<IMap>(assembly);
        }

        /// <summary>
        /// 配置全局过滤器
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="modelBuilder">映射生成器</param>
        /// <param name="entityType">实体类型</param>
        protected void ConfigureGlobalFilters<TEntity>(ModelBuilder modelBuilder, IMutableEntityType entityType)
            where TEntity : class
        {
            if (entityType.BaseType == null && ShouldFilterEntity<TEntity>(entityType))
            {
                var filterExpression = CreateFilterExpression<TEntity>();
                if (filterExpression != null)
                {
                    modelBuilder.Entity<TEntity>().HasQueryFilter(filterExpression);
                }
            }
        }

        /// <summary>
        /// 是否应该过滤实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <param name="entityType">实体类型</param>
        /// <returns></returns>
        protected virtual bool ShouldFilterEntity<TEntity>(IMutableEntityType entityType) where TEntity : class
        {
            if (typeof(IDelete).IsAssignableFrom(typeof(TEntity)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 创建过滤表达式
        /// </summary>
        /// <typeparam name="TEntity">实体类型</typeparam>
        /// <returns></returns>
        protected virtual Expression<Func<TEntity, bool>> CreateFilterExpression<TEntity>() where TEntity : class
        {
            Expression<Func<TEntity, bool>> expression = null;
            if (typeof(IDelete).IsAssignableFrom(typeof(TEntity)))
            {
                Expression<Func<TEntity, bool>> deleteFilter = e =>
                    !((IDelete) e).IsDeleted || ((IDelete) e).IsDeleted != IsDeleteFilterEnabled;
                expression = expression == null ? deleteFilter : CombineExpression(expression, deleteFilter);
            }

            return expression;
        }

        /// <summary>
        /// 合并表达式
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="expression1">表达式1</param>
        /// <param name="expression2">表达式2</param>
        /// <returns></returns>
        protected virtual Expression<Func<T, bool>> CombineExpression<T>(Expression<Func<T, bool>> expression1,
            Expression<Func<T, bool>> expression2)
        {
            var parameter = Expression.Parameter(typeof(T));

            var leftVisitor=new ReplaceExpressionVisitor(expression1.Parameters[0],parameter);
            var left = leftVisitor.Visit(expression1.Body);

            var rightVisitor = new ReplaceExpressionVisitor(expression2.Parameters[0], parameter);
            var right = rightVisitor.Visit(expression2.Body);

            return Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left, right), parameter);
        }

        /// <summary>
        /// 替换表达式访问器
        /// </summary>
        class ReplaceExpressionVisitor:ExpressionVisitor
        {
            /// <summary>
            /// 旧值
            /// </summary>
            private readonly Expression _oldValue;

            /// <summary>
            /// 新值
            /// </summary>
            private readonly Expression _newValue;

            /// <summary>
            /// 初始化一个<see cref="ReplaceExpressionVisitor"/>类型的实例
            /// </summary>
            /// <param name="oldValue">旧值</param>
            /// <param name="newValue">新值</param>
            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue;
                _newValue = newValue;
            }

            /// <summary>
            /// 访问
            /// </summary>
            /// <param name="node">表达式</param>
            /// <returns></returns>
            public override Expression Visit(Expression node)
            {
                if (node == _oldValue)
                {
                    return _newValue;
                }

                return base.Visit(node);
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
            CreationAuditedInitializer.Init(entry.Entity, GetSession());
        }

        /// <summary>
        /// 获取用户会话
        /// </summary>
        /// <returns></returns>
        protected virtual ISession GetSession()
        {
            return Session;
        }

        /// <summary>
        /// 初始化修改审计信息
        /// </summary>
        /// <param name="entry">输入实体</param>
        private void InitModificationAudited(EntityEntry entry)
        {
            ModificationAuditedInitializer.Init(entry.Entity, GetSession());
        }

        /// <summary>
        /// 拦截修改操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptModifiedOperation(EntityEntry entry)
        {
            InitModificationAudited(entry);
        }

        /// <summary>
        /// 拦截删除操作
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected virtual void InterceptDeletedOperation(EntityEntry entry)
        {
            DeletionAuditedInitializer.Init(entry.Entity,GetSession());
        }

        #endregion

        #region SaveChangesAsync(异步保存更改)
        /// <summary>
        /// 异步保存更改
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            SaveChangesBefore();
            return base.SaveChangesAsync(cancellationToken);
        }

        #endregion

        #region InitVersion(初始化版本号)

        /// <summary>
        /// 初始化版本号
        /// </summary>
        /// <param name="entry">输入实体</param>
        protected void InitVersion(EntityEntry entry)
        {
            if (!(entry.Entity is IAggregateRoot entity))
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
            var entityType = Model.FindEntityType(entity);
            return entityType?.FindAnnotation("Relational:TableName")?.Value.SafeString();
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
            var entityType = Model.FindEntityType(entity);
            return entityType?.FindAnnotation("Relational:Schema")?.Value.SafeString();
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
            var entityType = Model.FindEntityType(entity);
            var result = entityType?.GetProperty(property)?.FindAnnotation("Relational:ColumnName")?.Value.SafeString();
            return string.IsNullOrWhiteSpace(result) ? property : result;
        }

        #endregion

    }
}
