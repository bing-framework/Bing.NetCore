using System;
using System.Text;
using Bing.Auditing;
using Bing.Data;
using Bing.DependencyInjection;
using Bing.Extensions;
using Bing.Uow;
using Bing.Users;
using FreeSql;
using FreeSql.Aop;
using Microsoft.Extensions.DependencyInjection;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.FreeSQL
{
    /// <summary>
    /// 工作单元扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册MySql工作单元服务
        /// </summary>
        /// <typeparam name="TUnitOfWOrk">工作单元接口类型</typeparam>
        /// <typeparam name="TUnitOfWorkImplementation">工作单元实现类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="connection">连接字符串</param>
        /// <param name="setupAction">配置操作</param>
        /// <param name="freeSqlSetupAction">FreeSql配置操作</param>
        public static IServiceCollection AddMySqlUnitOfWork<TUnitOfWOrk, TUnitOfWorkImplementation>(this IServiceCollection services
            , string connection
            , Action<FreeSqlBuilder> setupAction = null
            , Action<IFreeSql> freeSqlSetupAction = null)
            where TUnitOfWOrk : class, IUnitOfWork
            where TUnitOfWorkImplementation : UnitOfWorkBase, TUnitOfWOrk
        {

            var freeSqlBuilder = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, connection)
                .UseLazyLoading(false);
            setupAction?.Invoke(freeSqlBuilder);

            var freeSql = freeSqlBuilder.Build();
            freeSqlSetupAction?.Invoke(freeSql);
            freeSql.Aop.AuditValue += (s, e) =>
            {
                // 乐观锁
                if (e.AuditValueType == AuditValueType.Insert || e.AuditValueType == AuditValueType.Update)
                {
                    if (e.Property.Name == AuditedPropertyConst.Version)
                        if (e.Value is byte[] bytes && bytes.Length == 0)
                            e.Value = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
                }
                // 时间
                if (e.AuditValueType == AuditValueType.Insert)
                {
                    if (e.Property.Name == AuditedPropertyConst.CreationTime ||
                        e.Property.Name == AuditedPropertyConst.ModificationTime)
                        e.Value = DateTime.Now;
                }
                if (e.AuditValueType == AuditValueType.Update)
                {
                    if (e.Property.Name == AuditedPropertyConst.ModificationTime)
                        e.Value = DateTime.Now;
                }
                // 用户
                if (e.Property.Name == AuditedPropertyConst.Creator ||
                    e.Property.Name == AuditedPropertyConst.Modifier ||
                    e.Property.Name == AuditedPropertyConst.CreatorId ||
                    e.Property.Name == AuditedPropertyConst.ModifierId)
                {
                    var currentUser = ServiceLocator.Instance?.GetService<ICurrentUser>();
                    if (currentUser == null || currentUser.UserId.IsEmpty())
                        return;
                    if (e.AuditValueType == AuditValueType.Insert)
                    {
                        if (e.Property.Name == AuditedPropertyConst.Creator ||
                            e.Property.Name == AuditedPropertyConst.Modifier)
                            e.Value = currentUser.GetFullName() ?? currentUser.GetUserName();
                        if (e.Property.Name == AuditedPropertyConst.CreatorId ||
                            e.Property.Name == AuditedPropertyConst.ModifierId)
                            e.Value = currentUser.GetUserId();
                    }
                    if (e.AuditValueType == AuditValueType.Update)
                    {
                        if (e.Property.Name == AuditedPropertyConst.Modifier)
                            e.Value = currentUser.GetFullName() ?? currentUser.GetUserName();
                        if (e.Property.Name == AuditedPropertyConst.ModifierId)
                            e.Value = currentUser.GetUserId();
                    }
                }
            };
            var freeSqlWrapper = new FreeSqlWrapper { Orm = freeSql };
            freeSql.GlobalFilter.Apply<ISoftDelete>("SoftDelete", x => x.IsDeleted == false);
            services.AddSingleton(freeSqlWrapper);
            services.AddScoped<TUnitOfWOrk, TUnitOfWorkImplementation>();
            return services;
        }

    }
}
