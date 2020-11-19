using System;
using Bing.Data;
using Bing.Uow;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using IUnitOfWork = Bing.Uow.IUnitOfWork;

namespace Bing.FreeSQL
{
    /// <summary>
    /// 工作单元扩展
    /// </summary>
    public static partial class MyUnitOfWorkExtensions
    {
        /// <summary>
        /// 注册MySql工作单元服务
        /// </summary>
        /// <typeparam name="TUnitOfWOrk">工作单元接口类型</typeparam>
        /// <typeparam name="TUnitOfWorkImplementation">工作单元实现类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="connection">连接字符串</param>
        /// <param name="setupAction">配置操作</param>
        public static IServiceCollection AddMySqlUnitOfWork<TUnitOfWOrk, TUnitOfWorkImplementation>(this IServiceCollection services, string connection, Action<FreeSqlBuilder> setupAction = null)
            where TUnitOfWOrk : class, IUnitOfWork
            where TUnitOfWorkImplementation : UnitOfWorkBase, TUnitOfWOrk
        {
            var freeSqlBuilder = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, connection)
                .UseLazyLoading(false);
            setupAction?.Invoke(freeSqlBuilder);
            var freeSql = freeSqlBuilder.Build();
            var freeSqlWrapper = new FreeSqlWrapper { Orm = freeSql };
            freeSql.GlobalFilter.Apply<ISoftDelete>("SoftDelete", x => x.IsDeleted == false);
            services.AddSingleton(freeSqlWrapper);
            services.AddScoped<TUnitOfWOrk, TUnitOfWorkImplementation>();
            return services;
        }

    }
}
