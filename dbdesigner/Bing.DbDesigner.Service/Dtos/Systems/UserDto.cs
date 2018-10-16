using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using Bing.Applications.Dtos;

namespace Bing.DbDesigner.Service.Dtos.Systems {
    /// <summary>
    /// 用户数据传输对象
    /// </summary>
    public class UserDto : DtoBase {
        /// <summary>
        /// 用户名
        /// </summary>
        [StringLength( 256, ErrorMessage = "用户名输入过长，不能超过256位" )]
        [Display( Name = "用户名" )]
        [DataMember]
        public string UserName { get; set; }
        /// <summary>
        /// 标准化用户名
        /// </summary>
        [StringLength( 256, ErrorMessage = "标准化用户名输入过长，不能超过256位" )]
        [Display( Name = "标准化用户名" )]
        [DataMember]
        public string NormalizedUserName { get; set; }
        /// <summary>
        /// 安全邮箱
        /// </summary>
        [StringLength( 256, ErrorMessage = "安全邮箱输入过长，不能超过256位" )]
        [Display( Name = "安全邮箱" )]
        [DataMember]
        public string Email { get; set; }
        /// <summary>
        /// 标准化邮箱
        /// </summary>
        [StringLength( 256, ErrorMessage = "标准化邮箱输入过长，不能超过256位" )]
        [Display( Name = "标准化邮箱" )]
        [DataMember]
        public string NormalizedEmail { get; set; }
        /// <summary>
        /// 邮箱已确认
        /// </summary>
        [Display( Name = "邮箱已确认" )]
        [DataMember]
        public bool? EmailConfirmed { get; set; }
        /// <summary>
        /// 安全手机
        /// </summary>
        [StringLength( 64, ErrorMessage = "安全手机输入过长，不能超过64位" )]
        [Display( Name = "安全手机" )]
        [DataMember]
        public string PhoneNumber { get; set; }
        /// <summary>
        /// 手机已确认
        /// </summary>
        [Display( Name = "手机已确认" )]
        [DataMember]
        public bool? PhoneNumberConfirmed { get; set; }
        /// <summary>
        /// 密码
        /// </summary>
        [StringLength( 256, ErrorMessage = "密码输入过长，不能超过256位" )]
        [Display( Name = "密码" )]
        [DataMember]
        public string Password { get; set; }
        /// <summary>
        /// 密码散列
        /// </summary>
        [Required(ErrorMessage = "密码散列不能为空")]
        [StringLength( 1024, ErrorMessage = "密码散列输入过长，不能超过1024位" )]
        [Display( Name = "密码散列" )]
        [DataMember]
        public string PasswordHash { get; set; }
        /// <summary>
        /// 安全码
        /// </summary>
        [StringLength( 256, ErrorMessage = "安全码输入过长，不能超过256位" )]
        [Display( Name = "安全码" )]
        [DataMember]
        public string SafePassword { get; set; }
        /// <summary>
        /// 安全码散列
        /// </summary>
        [StringLength( 1024, ErrorMessage = "安全码散列输入过长，不能超过1024位" )]
        [Display( Name = "安全码散列" )]
        [DataMember]
        public string SafePasswordHash { get; set; }
        /// <summary>
        /// 启用两阶段认证
        /// </summary>
        [Display( Name = "启用两阶段认证" )]
        [DataMember]
        public bool? TwoFactorEnabled { get; set; }
        /// <summary>
        /// 启用
        /// </summary>
        [Display( Name = "启用" )]
        [DataMember]
        public bool? Enabled { get; set; }
        /// <summary>
        /// 冻结时间
        /// </summary>
        [Display( Name = "冻结时间" )]
        [DataMember]
        public DateTime? DisabledTime { get; set; }
        /// <summary>
        /// 启用锁定
        /// </summary>
        [Display( Name = "启用锁定" )]
        [DataMember]
        public bool? LockoutEnabled { get; set; }
        /// <summary>
        /// 锁定截止
        /// </summary>
        [Display( Name = "锁定截止" )]
        [DataMember]
        public DateTimeOffset? LockoutEnd { get; set; }
        /// <summary>
        /// 登录失败次数
        /// </summary>
        [Required(ErrorMessage = "登录失败次数不能为空")]
        [Display( Name = "登录失败次数" )]
        [DataMember]
        public int AccessFailedCount { get; set; }
        /// <summary>
        /// 登录次数
        /// </summary>
        [Display( Name = "登录次数" )]
        [DataMember]
        public int? LoginCount { get; set; }
        /// <summary>
        /// 注册IP
        /// </summary>
        [StringLength( 30, ErrorMessage = "注册IP输入过长，不能超过30位" )]
        [Display( Name = "注册IP" )]
        [DataMember]
        public string RegisterIp { get; set; }
        /// <summary>
        /// 上次登录时间
        /// </summary>
        [Display( Name = "上次登录时间" )]
        [DataMember]
        public DateTime? LastLoginTime { get; set; }
        /// <summary>
        /// 上次登录IP
        /// </summary>
        [StringLength( 30, ErrorMessage = "上次登录IP输入过长，不能超过30位" )]
        [Display( Name = "上次登录IP" )]
        [DataMember]
        public string LastLoginIp { get; set; }
        /// <summary>
        /// 本次登录时间
        /// </summary>
        [Display( Name = "本次登录时间" )]
        [DataMember]
        public DateTime? CurrentLoginTime { get; set; }
        /// <summary>
        /// 本次登录IP
        /// </summary>
        [StringLength( 30, ErrorMessage = "本次登录IP输入过长，不能超过30位" )]
        [Display( Name = "本次登录IP" )]
        [DataMember]
        public string CurrentLoginIp { get; set; }
        /// <summary>
        /// 安全戳
        /// </summary>
        [StringLength( 1024, ErrorMessage = "安全戳输入过长，不能超过1024位" )]
        [Display( Name = "安全戳" )]
        [DataMember]
        public string SecunityStamp { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength( 500, ErrorMessage = "备注输入过长，不能超过500位" )]
        [Display( Name = "备注" )]
        [DataMember]
        public string Note { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display( Name = "创建时间" )]
        [DataMember]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [Display( Name = "创建人" )]
        [DataMember]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display( Name = "最后修改时间" )]
        [DataMember]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display( Name = "最后修改人" )]
        [DataMember]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [Display( Name = "是否删除" )]
        [DataMember]
        public bool? IsDeleted { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display( Name = "版本号" )]
        [DataMember]
        public Byte[] Version { get; set; }
    }
}