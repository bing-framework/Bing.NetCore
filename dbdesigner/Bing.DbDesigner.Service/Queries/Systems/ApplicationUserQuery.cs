using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Systems {
    /// <summary>
    /// 应用程序用户查询参数
    /// </summary>
    public class ApplicationUserQuery : QueryParameter {
        /// <summary>
        /// 应用程序标识
        /// </summary>
        [Display(Name="应用程序标识")]
        public Guid? ApplicationId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
    }
}