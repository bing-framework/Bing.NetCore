using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Projects {
    /// <summary>
    /// 用户解决方案查询参数
    /// </summary>
    public class UserSolutionQuery : QueryParameter {
        /// <summary>
        /// 用户解决方案标识
        /// </summary>
        [Display(Name="用户解决方案标识")]
        public Guid? UserSolutionId { get; set; }
        /// <summary>
        /// 解决方案标识
        /// </summary>
        [Display(Name="解决方案标识")]
        public Guid? SolutionId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
        /// <summary>
        /// 是否管理员
        /// </summary>
        [Display(Name="是否管理员")]
        public bool? IsAdmin { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name="启用")]
        public bool? Enabled { get; set; }
        /// <summary>
        /// 起始创建时间
        /// </summary>
        [Display( Name = "起始创建时间" )]
        public DateTime? BeginCreationTime { get; set; }
        /// <summary>
        /// 结束创建时间
        /// </summary>
        [Display( Name = "结束创建时间" )]
        public DateTime? EndCreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name="创建人")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 起始最后修改时间
        /// </summary>
        [Display( Name = "起始最后修改时间" )]
        public DateTime? BeginLastModificationTime { get; set; }
        /// <summary>
        /// 结束最后修改时间
        /// </summary>
        [Display( Name = "结束最后修改时间" )]
        public DateTime? EndLastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name="最后修改人")]
        public Guid? LastModifierId { get; set; }
    }
}