using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Domains.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 用户
    /// </summary>
    [Display(Name = "用户")]
    public partial class User : Bing.Permissions.Identity.Models.UserBase<User, Guid>, IDelete,IAuditedWithNameObject
    {
        /// <summary>
        /// 初始化一个<see cref="User"/>类型的实例
        /// </summary>
        public User() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="User"/>类型的实例
        /// </summary>
        /// <param name="id">用户标识</param>
        public User(Guid id) : base(id) { }

        /// <summary>
        /// 名称
        ///</summary>
        [Display(Name = "名称")]
        [StringLength( 256, ErrorMessage = "名称输入过长，不能超过256位" )]
        public string Name { get; set; }
    
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
            AddDescription( t => t.Name );
            AddDescription( t => t.Nickname );
            AddDescription( t => t.UserName );
            AddDescription( t => t.NormalizedUserName );
            AddDescription( t => t.Email );
            AddDescription( t => t.NormalizedEmail );
            AddDescription( t => t.EmailConfirmed );
            AddDescription( t => t.PhoneNumber );
            AddDescription( t => t.PhoneNumberConfirmed );
            AddDescription( t => t.Password );
            AddDescription( t => t.PasswordHash );
            AddDescription( t => t.SecurityStamp );
            AddDescription( t => t.SafePassword );
            AddDescription( t => t.SafePasswordHash );
            AddDescription( t => t.TwoFactorEnabled );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.DisabledTime );
            AddDescription( t => t.LockoutEnd );
            AddDescription( t => t.LockoutEnabled );
            AddDescription( t => t.IsSystem );
            AddDescription( t => t.AccessFailedCount );
            AddDescription( t => t.LoginCount );
            AddDescription( t => t.RegisterIp );
            AddDescription( t => t.LastLoginTime );
            AddDescription( t => t.LastLoginIp );
            AddDescription( t => t.CurrentLoginTime );
            AddDescription( t => t.CurrentLoginIp );
            AddDescription( t => t.Remark );
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
        protected override void AddChanges( User other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Nickname, other.Nickname );
            AddChange( t => t.UserName, other.UserName );
            AddChange( t => t.NormalizedUserName, other.NormalizedUserName );
            AddChange( t => t.Email, other.Email );
            AddChange( t => t.NormalizedEmail, other.NormalizedEmail );
            AddChange( t => t.EmailConfirmed, other.EmailConfirmed );
            AddChange( t => t.PhoneNumber, other.PhoneNumber );
            AddChange( t => t.PhoneNumberConfirmed, other.PhoneNumberConfirmed );
            AddChange( t => t.Password, other.Password );
            AddChange( t => t.PasswordHash, other.PasswordHash );
            AddChange( t => t.SecurityStamp, other.SecurityStamp );
            AddChange( t => t.SafePassword, other.SafePassword );
            AddChange( t => t.SafePasswordHash, other.SafePasswordHash );
            AddChange( t => t.TwoFactorEnabled, other.TwoFactorEnabled );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.DisabledTime, other.DisabledTime );
            AddChange( t => t.LockoutEnd, other.LockoutEnd );
            AddChange( t => t.LockoutEnabled, other.LockoutEnabled );
            AddChange( t => t.IsSystem, other.IsSystem );
            AddChange( t => t.AccessFailedCount, other.AccessFailedCount );
            AddChange( t => t.LoginCount, other.LoginCount );
            AddChange( t => t.RegisterIp, other.RegisterIp );
            AddChange( t => t.LastLoginTime, other.LastLoginTime );
            AddChange( t => t.LastLoginIp, other.LastLoginIp );
            AddChange( t => t.CurrentLoginTime, other.CurrentLoginTime );
            AddChange( t => t.CurrentLoginIp, other.CurrentLoginIp );
            AddChange( t => t.Remark, other.Remark );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.LastModifier, other.LastModifier );
        }
    }
}
