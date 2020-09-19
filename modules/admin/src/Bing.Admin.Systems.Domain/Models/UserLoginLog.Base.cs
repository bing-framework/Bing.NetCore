using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Auditing;
using Bing.Domains;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 用户登录日志
    /// </summary>
    [Display(Name = "用户登录日志")]
    public partial class UserLoginLog : AggregateRoot<UserLoginLog>
    {
        /// <summary>
        /// 初始化一个<see cref="UserLoginLog"/>类型的实例
        /// </summary>
        public UserLoginLog() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="UserLoginLog"/>类型的实例
        /// </summary>
        /// <param name="id">用户登录日志标识</param>
        public UserLoginLog(Guid id) : base(id) { }

        /// <summary>
        /// 用户编号
        ///</summary>
        [Display(Name = "用户编号")]
        [Required(ErrorMessage = "用户编号不能为空")]
        public Guid UserId { get; set; }
    
        /// <summary>
        /// 名称
        ///</summary>
        [Display(Name = "名称")]
        [StringLength( 250, ErrorMessage = "名称输入过长，不能超过250位" )]
        public string Name { get; set; }
    
        /// <summary>
        /// 登录IP
        ///</summary>
        [Display(Name = "登录IP")]
        [StringLength( 30, ErrorMessage = "登录IP输入过长，不能超过30位" )]
        public string Ip { get; set; }
    
        /// <summary>
        /// 用户代理
        ///</summary>
        [Display(Name = "用户代理")]
        [StringLength( 1000, ErrorMessage = "用户代理输入过长，不能超过1000位" )]
        public string UserAgent { get; set; }
    
        /// <summary>
        /// 退出登录时间
        ///</summary>
        [Display(Name = "退出登录时间")]
        public DateTime? LogoutTime { get; set; }
    
        /// <summary>
        /// 创建时间
        ///</summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }
    
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription( t => t.Id );
            AddDescription( t => t.UserId );
            AddDescription( t => t.Name );
            AddDescription( t => t.Ip );
            AddDescription( t => t.UserAgent );
            AddDescription( t => t.LogoutTime );
            AddDescription( t => t.CreationTime );
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( UserLoginLog other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.UserId, other.UserId );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Ip, other.Ip );
            AddChange( t => t.UserAgent, other.UserAgent );
            AddChange( t => t.LogoutTime, other.LogoutTime );
            AddChange( t => t.CreationTime, other.CreationTime );
        }
    }
}
