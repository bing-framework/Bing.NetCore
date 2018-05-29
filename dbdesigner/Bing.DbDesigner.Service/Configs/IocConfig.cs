using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Bing.Datas.UnitOfWorks;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Data.UnitOfWorks.SqlServer;
using Bing.Dependency;

namespace Bing.DbDesigner.Service.Configs
{
    /// <summary>
    /// 依赖注入配置
    /// </summary>
    public class IocConfig:ConfigBase
    {
        /// <summary>
        /// 加载配置
        /// </summary>
        protected override void Load(ContainerBuilder builder)
        {
            //LoadInfrastructure(builder);
            //LoadRepositories(builder);
            //LoadDomainServices(builder);
            //LoadApplicationServices(builder);
        }

        /// <summary>
        /// 加载基础设施
        /// </summary>
        protected virtual void LoadInfrastructure(ContainerBuilder builder)
        {
            builder.AddScoped<IUnitOfWorkManager, UnitOfWorkManager>();
            builder.AddScoped<IDbDesignerUnitOfWork, DbDesignerUnitOfWork>();
        }

        /// <summary>
        /// 加载仓储
        /// </summary>
        protected virtual void LoadRepositories(ContainerBuilder builder)
        {
        }

        /// <summary>
        /// 加载领域服务
        /// </summary>
        protected virtual void LoadDomainServices(ContainerBuilder builder)
        {
        }

        /// <summary>
        /// 加载应用服务
        /// </summary>
        protected virtual void LoadApplicationServices(ContainerBuilder builder)
        {            
        }
    }
}
