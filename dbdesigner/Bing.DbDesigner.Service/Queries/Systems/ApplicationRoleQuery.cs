using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Systems {
    /// <summary>
    /// 应用程序角色查询参数
    /// </summary>
    public class ApplicationRoleQuery : QueryParameter {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Display(Name="应用程序标识")]
        public Guid? ApplicationId { get; set; }
        /// <summary>
        /// 角色标识
        /// </summary>
        [Display(Name="角色标识")]
        public Guid? RoleId { get; set; }
    }
}