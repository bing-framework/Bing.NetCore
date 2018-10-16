using Bing.Applications;
using Bing.DbDesigner.Service.Dtos.Projects;
using Bing.DbDesigner.Service.Queries.Projects;

namespace Bing.DbDesigner.Service.Abstractions.Projects {
    /// <summary>
    /// 项目服务
    /// </summary>
    public interface IProjectService : ICrudService<ProjectDto, ProjectQuery> {
    }
}