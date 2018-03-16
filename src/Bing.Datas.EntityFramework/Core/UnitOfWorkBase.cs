using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Logs;
using Bing.Datas.UnitOfWorks;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Sessions;
using Bing.Exceptions;
using Bing.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Bing.Datas.EntityFramework.Core
{
    /// <summary>
    /// 工作单元
    /// </summary>
    public abstract class UnitOfWorkBase:DbContext,IUnitOfWork
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
            Session = Bing.Domains.Sessions.Session.Null;
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
                return Log.GetLog(EfLog.TRACE_LOG_NAME);
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
            return Bing.Utils.Helpers.Reflection.GetTypesByInterface<IMap>(assembly);
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
    }
}
