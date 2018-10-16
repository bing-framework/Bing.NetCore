using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Commons;
using Bing.DbDesigner.Service.Queries.Commons;

namespace Bing.DbDesigner.Service.Abstractions.Commons {
    /// <summary>
    /// 系统配置服务
    /// </summary>
    public interface IConfigurationService : ICrudService<ConfigurationDto, ConfigurationQuery> {
    }
}