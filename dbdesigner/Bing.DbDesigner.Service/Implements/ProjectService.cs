using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Domains.Repositories;
using Bing.DbDesigner.Service.Abstractions;
using Bing.DbDesigner.Service.Dtos;

namespace Bing.DbDesigner.Service.Implements
{
    public class ProjectService:ServiceBase,IProjectService
    {
        /// <summary>
        /// 工作单元
        /// </summary>
        public IDbDesignerUnitOfWork UnitOfWork { get; set; }

        /// <summary>
        /// 项目仓储
        /// </summary>
        public IProjectRepository ProjectRepository { get; set; }
        
        public ProjectService(IDbDesignerUnitOfWork unitOfWork, IProjectRepository projectRepository)
        {
            UnitOfWork = unitOfWork;
            ProjectRepository = projectRepository;
        }
        public virtual async Task SaveProject(SaveProjectRequest request)
        {
            await UnitOfWork.CommitAsync();
        }
    }
}
