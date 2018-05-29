using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.DbDesigner.Data;
using Bing.DbDesigner.Domains.Models;
using Bing.DbDesigner.Domains.Repositories;
using Bing.DbDesigner.Service.Abstractions;
using Bing.DbDesigner.Service.Dtos;
using Bing.Extensions.AutoMapper;

namespace Bing.DbDesigner.Service.Implements
{
    /// <summary>
    /// 项目服务
    /// </summary>
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

        /// <summary>
        /// 保存项目
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public virtual async Task SaveProject(SaveProjectRequest request)
        {
            var project = ToEntity(request);
            project.Init();
            await ProjectRepository.AddAsync(project);
            await UnitOfWork.CommitAsync();
        }

        /// <summary>
        /// 转换为实体
        /// </summary>
        /// <returns></returns>
        private Project ToEntity(SaveProjectRequest request)
        {
            return request.MapTo<Project>();
        }
    }
}
