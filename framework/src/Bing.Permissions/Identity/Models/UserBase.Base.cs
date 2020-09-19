using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Permissions.Identity.Models
{
    /// <summary>
    /// 用户基类
    /// </summary>
    /// <typeparam name="TUser">用户类型你</typeparam>
    /// <typeparam name="TKey">用户标识类型</typeparam>
    [Display(Name = "用户")]
    public abstract partial class UserBase<TUser, TKey> : AggregateRoot<TUser, TKey>, ISoftDelete, IAuditedObject where TUser : UserBase<TUser, TKey>
    {
        #region 属性

        /// <summary>
        /// 用户名
        /// </summary>
        [Display(Name = "用户名")]
        [StringLength(256, ErrorMessage = "用户名输入过长，不能超过256位")]
        public string UserName { get; set; }

        /// <summary>
        /// 标准化用户名
        /// </summary>
        [Display(Name = "标准化用户名")]
        [StringLength(256, ErrorMessage = "标准化用户名输入过长，不能超过256位")]
        public string NormalizedUserName { get; set; }

        [Display(Name = "昵称")]
        [StringLength(256, ErrorMessage = "昵称输入过长，不能超过256位")]
        public string Nickname { get; set; }

        /// <summary>
        /// 电子邮箱
        /// </summary>
        [Display(Name = "电子邮箱")]
        [StringLength(256, ErrorMessage = "电子邮箱s输入过长，不能超过256位")]
        public string Email { get; set; }

        /// <summary>
        /// 标准化的电子邮箱
        /// </summary>
        [Display(Name = "标准化的电子邮箱")]
        [StringLength(256, ErrorMessage = "标准化的电子邮箱输入过长，不能超过256位")]
        public string NormalizedEmail { get; set; }

        /// <summary>
        /// 邮箱已确认
        /// </summary>
        [Display(Name = "邮箱已确认")]
        public bool EmailConfirmed { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        [Display(Name = "手机号码")]
        [StringLength(64, ErrorMessage = "手机号码输入过长，不能超过64位")]
        public string PhoneNumber { get; set; }

        /// <summary>
        /// 手机号码已确认
        /// </summary>
        [Display(Name = "手机已确认")]
        public bool PhoneNumberConfirmed { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        [Display(Name = "密码")]
        [StringLength(256, ErrorMessage = "密码输入过长，不能超过256位")]
        public string Password { get; set; }

        /// <summary>
        /// 密码哈希值
        /// </summary>
        [Display(Name = "密码哈希值")]
        [StringLength(1024, ErrorMessage = "密码哈希值输入过长，不能超过1024位")]
        public string PasswordHash { get; set; }

        /// <summary>
        /// 安全码
        /// </summary>
        [Display(Name = "安全码")]
        [StringLength(256, ErrorMessage = "安全码输入过长，不能超过256位")]
        public string SafePassword { get; set; }

        /// <summary>
        /// 安全码哈希值
        /// </summary>
        [Display(Name = "安全码哈希值")]
        [StringLength(1024, ErrorMessage = "安全码哈希值输入过长，不能超过1024位")]
        public string SafePasswordHash { get; set; }

        /// <summary>
        /// 安全戳
        /// </summary>
        [Display(Name = "安全戳")]
        [StringLength(1024, ErrorMessage = "安全戳输入过长，不能超过1024位")]
        public string SecurityStamp { get; set; }

        /// <summary>
        /// 启用双因子身份验证
        /// </summary>
        [Display(Name = "启用双因子身份验证")]
        public bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool Enabled { get; set; }

        /// <summary>
        /// 冻结时间
        /// </summary>
        [Display(Name = "冻结时间")]
        public DateTime? DisabledTime { get; set; }

        /// <summary>
        /// 锁定截止时间
        /// </summary>
        [Display(Name = "锁定截止时间")]
        public DateTimeOffset? LockoutEnd { get; set; }

        /// <summary>
        /// 启用锁定
        /// </summary>
        [Display(Name = "启用锁定")]
        public bool LockoutEnabled { get; set; }

        /// <summary>
        /// 是否系统用户
        /// </summary>
        [Display(Name = "是否系统用户")]
        public bool IsSystem { get; set; }

        /// <summary>
        /// 登录失败次数
        /// </summary>
        [Display(Name = "登录失败次数")]
        public int AccessFailedCount { get; set; }

        /// <summary>
        /// 登录次数
        /// </summary>
        [Display(Name = "登录次数")]
        public int? LoginCount { get; set; }

        /// <summary>
        /// 注册Ip
        /// </summary>
        [Display(Name = "注册Ip")]
        [StringLength(30, ErrorMessage = "注册Ip输入过长，不能超过30位")]
        public string RegisterIp { get; set; }

        /// <summary>
        /// 上次登录时间
        /// </summary>
        [Display(Name = "上次登录时间")]
        public DateTime? LastLoginTime { get; set; }

        /// <summary>
        /// 上次登录Ip
        /// </summary>
        [Display(Name = "上次登录Ip")]
        [StringLength(30, ErrorMessage = "上次登录Ip输入过长，不能超过30位")]
        public string LastLoginIp { get; set; }

        /// <summary>
        /// 本次登录时间
        /// </summary>
        [Display(Name = "本次登录时间")]
        public DateTime? CurrentLoginTime { get; set; }

        /// <summary>
        /// 本次登录Ip
        /// </summary>
        [Display(Name = "本次登录Ip")]
        [StringLength(30, ErrorMessage = "本次登录Ip输入过长，不能超过30位")]
        public string CurrentLoginIp { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        public string Remark { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name = "创建人")]
        public Guid? CreatorId { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name = "最后修改人")]
        public Guid? LastModifierId { get; set; }

        /// <summary>
        /// 是否删除
        /// </summary>
        [Display(Name = "是否删除")]
        public bool IsDeleted { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected UserBase(TKey id) : base(id) { }

        #endregion

        #region AddDescriptions(添加描述)

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription(t => t.Id);
            AddDescription(t => t.UserName);
            AddDescription(t => t.NormalizedUserName);
            AddDescription(t => t.Email);
            AddDescription(t => t.NormalizedEmail);
            AddDescription(t => t.EmailConfirmed);
            AddDescription(t => t.PhoneNumber);
            AddDescription(t => t.PhoneNumberConfirmed);
            AddDescription(t => t.Password);
            AddDescription(t => t.PasswordHash);
            AddDescription(t => t.SafePassword);
            AddDescription(t => t.SafePasswordHash);
            AddDescription(t => t.LockoutEnabled);
            AddDescription(t => t.IsSystem);
            AddDescription(t => t.LockoutEnd);
            AddDescription(t => t.LastLoginTime);
            AddDescription(t => t.LastLoginIp);
            AddDescription(t => t.CurrentLoginTime);
            AddDescription(t => t.CurrentLoginIp);
            AddDescription(t => t.LoginCount);
            AddDescription(t => t.AccessFailedCount);
            AddDescription(t => t.TwoFactorEnabled);
            AddDescription(t => t.Enabled);
            AddDescription(t => t.DisabledTime);
            AddDescription(t => t.RegisterIp);
            AddDescription(t => t.Remark);
            AddDescription(t => t.CreationTime);
            AddDescription(t => t.CreatorId);
            AddDescription(t => t.LastModificationTime);
            AddDescription(t => t.LastModifierId);
        }

        #endregion

        #region AddChanges(添加变更列表)

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges(TUser other)
        {
            AddChange(t => t.Id, other.Id);
            AddChange(t => t.UserName, other.UserName);
            AddChange(t => t.NormalizedUserName, other.NormalizedUserName);
            AddChange(t => t.Email, other.Email);
            AddChange(t => t.NormalizedEmail, other.NormalizedEmail);
            AddChange(t => t.EmailConfirmed, other.EmailConfirmed);
            AddChange(t => t.PhoneNumber, other.PhoneNumber);
            AddChange(t => t.PhoneNumberConfirmed, other.PhoneNumberConfirmed);
            AddChange(t => t.Password, other.Password);
            AddChange(t => t.PasswordHash, other.PasswordHash);
            AddChange(t => t.SafePassword, other.SafePassword);
            AddChange(t => t.SafePasswordHash, other.SafePasswordHash);
            AddChange(t => t.LockoutEnabled, other.LockoutEnabled);
            AddChange(t => t.LockoutEnd, other.LockoutEnd);
            AddChange(t => t.IsSystem, other.IsSystem);
            AddChange(t => t.LastLoginTime, other.LastLoginTime);
            AddChange(t => t.LastLoginIp, other.LastLoginIp);
            AddChange(t => t.CurrentLoginTime, other.CurrentLoginTime);
            AddChange(t => t.CurrentLoginIp, other.CurrentLoginIp);
            AddChange(t => t.LoginCount, other.LoginCount);
            AddChange(t => t.AccessFailedCount, other.AccessFailedCount);
            AddChange(t => t.TwoFactorEnabled, other.TwoFactorEnabled);
            AddChange(t => t.Enabled, other.Enabled);
            AddChange(t => t.DisabledTime, other.DisabledTime);
            AddChange(t => t.RegisterIp, other.RegisterIp);
            AddChange(t => t.Remark, other.Remark);
            AddChange(t => t.CreationTime, other.CreationTime);
            AddChange(t => t.CreatorId, other.CreatorId);
            AddChange(t => t.LastModificationTime, other.LastModificationTime);
            AddChange(t => t.LastModifierId, other.LastModifierId);
        }

        #endregion
    }
}
