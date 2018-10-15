using Bing.DbDesigner.Commons.Domain.Models;
using Bing.DbDesigner.Commons.Domain.Repositories;
using Bing.Datas.EntityFramework.Core;

namespace Bing.DbDesigner.Data.Repositories.Commons {
    /// <summary>
    /// 系统配置仓储
    /// </summary>
    public class ConfigurationRepository : RepositoryBase<Configuration>, IConfigurationRepository {
        /// <summary>
        /// 初始化系统配置仓储
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ConfigurationRepository( IDbDesignerUnitOfWork unitOfWork ) : base( unitOfWork ) {
        }
    }
}