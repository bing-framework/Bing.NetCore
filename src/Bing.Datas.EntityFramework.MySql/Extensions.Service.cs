using Bing.Datas.Configs;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.EntityFramework.Extensions;
using Bing.Datas.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Datas.EntityFramework.MySql
{
    /// <summary>
    /// 数据服务 扩展
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        /// 注册MySql工作单元服务
        /// </summary>
        /// <typeparam name="TService">工作单元接口类型</typeparam>
        /// <typeparam name="TImplementation">工作单元实现类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="connection">连接字符串</param>
        /// <param name="level">EF日志级别</param>
        /// <returns></returns>
        public static IServiceCollection AddSqlServerUnitOfWork<TService, TImplementation>(
            this IServiceCollection services,
            string connection, DataLogLevel level = DataLogLevel.Sql)
            where TService : class, IUnitOfWork
            where TImplementation : UnitOfWorkBase, TService
        {
            DataConfig.LogLevel = level;
            return services.AddUnitOfWork<TService, TImplementation>(builder =>
            {
                builder.UseMySql(connection);
            });
        }
    }
}
