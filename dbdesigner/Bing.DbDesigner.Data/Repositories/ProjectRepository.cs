using System;
using System.Collections.Generic;
using System.Text;
using Bing.Datas.EntityFramework.Core;
using Bing.Datas.UnitOfWorks;
using Bing.DbDesigner.Domains.Models;
using Bing.DbDesigner.Domains.Repositories;

namespace Bing.DbDesigner.Data.Repositories
{
    /// <summary>
    /// 项目仓储
    /// </summary>
    public class ProjectRepository:RepositoryBase<Project>,IProjectRepository
    {
        /// <summary>
        /// 初始化一个<see cref="ProjectRepository"/>类型的实例
        /// </summary>
        /// <param name="unitOfWork">工作单元</param>
        public ProjectRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }
}
