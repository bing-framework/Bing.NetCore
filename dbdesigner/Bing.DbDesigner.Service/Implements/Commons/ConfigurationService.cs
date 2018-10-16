using Bing;
using Bing.Extensions.AutoMapper;
using Bing.Domains.Repositories;
using Bing.Datas.Queries;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;
using Bing.DbDesigner.Service.Abstractions.Commons;

namespace Bing.DbDesigner.Service.Implements.Commons {
    /// <summary>
    /// 系统配置服务
    /// </summary>
    public class ConfigurationService : CrudServiceBase<Configuration, ConfigurationDto, ConfigurationQuery>, IConfigurationService {
        /// <summary>
        /// 初始化系统配置服务
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        /// <param name="configurationRepository">系统配置仓储</param>
        public ConfigurationService( IDbDesignerUnitOfWork unitOfWork, IConfigurationRepository configurationRepository )
            : base( unitOfWork, configurationRepository ) {
            ConfigurationRepository = configurationRepository;
        }
        
        /// <summary>
        /// 系统配置仓储
        /// </summary>
        public IConfigurationRepository ConfigurationRepository { get; set; }
        
        /// <summary>
        /// 创建查询对象
        /// </summary>
        /// <param name="param">查询参数</param>
        protected override IQueryBase<Configuration> CreateQuery( ConfigurationQuery param ) {
            return new Query<Configuration>( param );
        }
    }
}