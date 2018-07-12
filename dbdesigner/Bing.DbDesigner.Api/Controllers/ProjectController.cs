using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bing.DbDesigner.Service.Abstractions;
using Bing.DbDesigner.Service.Dtos;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.DbDesigner.Api.Controllers
{
    /// <summary>
    /// 项目 相关API
    /// </summary>
    public class ProjectController: ApiControllerBase
    {
        /// <summary>
        /// 项目服务
        /// </summary>
        public IProjectService ProjectService { get; }

        public ProjectController(IProjectService projectService)
        {
            ProjectService = projectService;
        }

        /// <summary>
        /// 保存项目
        /// </summary>
        /// <param name="request">参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> SaveAsync([FromBody] SaveProjectRequest request)
        {
            await ProjectService.SaveProject(request);
            return Success();
        }
    }
}
