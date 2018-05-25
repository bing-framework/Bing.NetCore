using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Aspects;
using Bing.DbDesigner.Service.Dtos;
using Bing.Validations.Aspects;

namespace Bing.DbDesigner.Service.Abstractions
{
    /// <summary>
    /// 项目服务
    /// </summary>
    public interface IProjectService:IService
    {
        /// <summary>
        /// 保存项目
        /// </summary>
        /// <param name="request">参数</param>
        /// <returns></returns>
        Task SaveProject([NotNull] [Valid] SaveProjectRequest request);
    }
}
