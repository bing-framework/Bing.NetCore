using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Admin.Domain.Shared.Enums;
using Bing.Utils;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Auditing;
using Bing.Domains;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 应用程序
    /// </summary>
    [Display(Name = "应用程序")]
    public partial class Application : Bing.Permissions.Identity.Models.ApplicationBase<Application, Guid>, IHasCreator, IHasModifier
    {
        /// <summary>
        /// 初始化一个<see cref="Application"/>类型的实例
        /// </summary>
        public Application() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="Application"/>类型的实例
        /// </summary>
        /// <param name="id">应用程序标识</param>
        public Application(Guid id) : base(id) { }


        /// <summary>
        /// 创建人
        ///</summary>
        [Display(Name = "创建人")]
        [StringLength(200, ErrorMessage = "创建人输入过长，不能超过200位")]
        public string Creator { get; set; }

        /// <summary>
        /// 最后修改人
        ///</summary>
        [Display(Name = "最后修改人")]
        [StringLength(200, ErrorMessage = "最后修改人输入过长，不能超过200位")]
        public string LastModifier { get; set; }

        /// <summary>
        /// 应用程序类型
        /// </summary>
        [DisplayName("应用程序类型")]
        public ApplicationType ApplicationType { get; set; }

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription(t => t.Id);
            AddDescription(t => t.Code);
            AddDescription(t => t.Name);
            AddDescription(t => t.RegisterEnabled);
            AddDescription(t => t.Enabled);
            AddDescription(t => t.Remark);
            AddDescription(t => t.CreationTime);
            AddDescription(t => t.CreatorId);
            AddDescription(t => t.Creator);
            AddDescription(t => t.LastModificationTime);
            AddDescription(t => t.LastModifierId);
            AddDescription(t => t.LastModifier);
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges(Application other)
        {
            AddChange(t => t.Id, other.Id);
            AddChange(t => t.Code, other.Code);
            AddChange(t => t.Name, other.Name);
            AddChange(t => t.RegisterEnabled, other.RegisterEnabled);
            AddChange(t => t.Enabled, other.Enabled);
            AddChange(t => t.Remark, other.Remark);
            AddChange(t => t.CreationTime, other.CreationTime);
            AddChange(t => t.CreatorId, other.CreatorId);
            AddChange(t => t.Creator, other.Creator);
            AddChange(t => t.LastModificationTime, other.LastModificationTime);
            AddChange(t => t.LastModifierId, other.LastModifierId);
            AddChange(t => t.LastModifier, other.LastModifier);
        }
    }
}
