using System.ComponentModel;
using Bing.Admin.Data;
using Bing.Core.Modularity;
using Bing.Data.Enums;
using Bing.Datas.Dapper;
using Bing.FreeSQL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Admin.Modules
{
    /// <summary>
    /// FreeSQL模块
    /// </summary>
    [Description("FreeSQL模块")]
    public class FreeSqlModule : BingModule
    {
        /// <summary>
        /// 模块级别。级别越小越先启动
        /// </summary>
        public override ModuleLevel Level => ModuleLevel.Framework;

        /// <summary>
        /// 模块启动顺序。模块启动的顺序先按级别启动，同一级别内部再按此顺序启动，
        /// 级别默认为0，表示无依赖，需要在同级别有依赖顺序的时候，再重写为>0的顺序值
        /// </summary>
        public override int Order => 1;

        /// <summary>
        /// 添加服务。将模块服务添加到依赖注入服务容器中
        /// </summary>
        /// <param name="services">服务集合</param>
        public override IServiceCollection AddServices(IServiceCollection services)
        {
            var configuration = services.GetConfiguration();
            var connectionStr = configuration.GetConnectionString("DefaultConnection");
            // 注册工作单元
            services.AddMySqlUnitOfWork<IAdminUnitOfWork, Bing.Admin.Data.UnitOfWorks.MySql.AdminUnitOfWork>(connectionStr);
            services.AddMySqlUnitOfWork<IAdminReadonlyUnitOfWork, Bing.Admin.Data.UnitOfWorks.MySql.AdminReadonlyUnitOfWork>(connectionStr);
            // 注册SqlQuery
            //services.AddSqlQuery<Bing.Admin.Data.UnitOfWorks.MySql.AdminUnitOfWork, Bing.Admin.Data.UnitOfWorks.MySql.AdminUnitOfWork>(options =>
            //    {
            //        options.DatabaseType = DatabaseType.MySql;
            //        options.IsClearAfterExecution = true;
            //        //options.LogLevel = DataLogLevel.Off;
            //    });
            services.AddSqlQuery<Bing.Admin.Data.UnitOfWorks.MySql.AdminReadonlyUnitOfWork, Bing.Admin.Data.UnitOfWorks.MySql.AdminReadonlyUnitOfWork>(options =>
            {
                options.DatabaseType = DatabaseType.MySql;
                options.IsClearAfterExecution = true;
                //options.LogLevel = DataLogLevel.Off;
            });
            // 注册SqlExecutor
            services.AddSqlExecutor();
            return services;
        }
    }
}
