using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Domains.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 角色
    /// </summary>
    [Display(Name = "角色")]
    public partial class Role : Bing.Permissions.Identity.Models.RoleBase<Role, Guid, Guid?>, IDelete,IAuditedWithNameObject
    {
        /// <summary>
        /// 初始化一个<see cref="Role"/>类型的实例
        /// </summary>
        public Role() : this(Guid.Empty, "", 0) { }

        /// <summary>
        /// 初始化一个<see cref="Role"/>类型的实例
        /// </summary>
        /// <param name="id">角色标识</param>
        /// <param name="path">路径</param>
        /// <param name="level">级数</param>
        public Role(Guid id, string path, int level) : base(id, path, level) { }

        /// <summary>
        /// 创建人
        ///</summary>
        [Display(Name = "创建人")]
        [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
        public string Creator { get; set; }
    
        /// <summary>
        /// 最后修改人
        ///</summary>
        [Display(Name = "最后修改人")]
        [StringLength( 200, ErrorMessage = "最后修改人输入过长，不能超过200位" )]
        public string LastModifier { get; set; }
    
        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription( t => t.Id );
            AddDescription( t => t.Code );
            AddDescription( t => t.Name );
            AddDescription( t => t.NormalizedName );
            AddDescription( t => t.Type );
            AddDescription( t => t.IsAdmin );
            AddDescription( t => t.IsDefault );
            AddDescription( t => t.IsSystem );
            AddDescription( t => t.ParentId );
            AddDescription( t => t.Path );
            AddDescription( t => t.Level );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.SortId );
            AddDescription( t => t.Remark );
            AddDescription( t => t.PinYin );
            AddDescription( t => t.Sign );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.Creator );
            AddDescription( t => t.LastModificationTime );
            AddDescription( t => t.LastModifierId );
            AddDescription( t => t.LastModifier );
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( Role other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.NormalizedName, other.NormalizedName );
            AddChange( t => t.Type, other.Type );
            AddChange( t => t.IsAdmin, other.IsAdmin );
            AddChange( t => t.IsDefault, other.IsDefault );
            AddChange( t => t.IsSystem, other.IsSystem );
            AddChange( t => t.ParentId, other.ParentId );
            AddChange( t => t.Path, other.Path );
            AddChange( t => t.Level, other.Level );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.Remark, other.Remark );
            AddChange( t => t.PinYin, other.PinYin );
            AddChange( t => t.Sign, other.Sign );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.LastModifier, other.LastModifier );
        }
    }
}
