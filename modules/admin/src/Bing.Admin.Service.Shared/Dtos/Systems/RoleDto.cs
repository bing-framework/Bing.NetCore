using System;
using System.ComponentModel.DataAnnotations;
using Bing.Application.Dtos;

namespace Bing.Admin.Service.Shared.Dtos.Systems
{
    /// <summary>
    /// 角色数据传输对象
    /// </summary>
    public class RoleDto : TreeDto<RoleDto>
    {
        /// <summary>
        /// 角色编码
        /// </summary>
        [Required(ErrorMessage = "角色编码不能为空")]
        [StringLength(256, ErrorMessage = "角色编码输入过长，不能超过256位")]
        [Display(Name = "角色编码")]
        public string Code { get; set; }
        /// <summary>
        /// 角色名称
        /// </summary>
        [Required(ErrorMessage = "角色名称不能为空")]
        [StringLength(256, ErrorMessage = "角色名称输入过长，不能超过256位")]
        [Display(Name = "角色名称")]
        public string Name { get; set; }
        /// <summary>
        /// 标准化角色名称
        /// </summary>
        [Required(ErrorMessage = "标准化角色名称不能为空")]
        [StringLength(256, ErrorMessage = "标准化角色名称输入过长，不能超过256位")]
        [Display(Name = "标准化角色名称")]
        public string NormalizedName { get; set; }
        /// <summary>
        /// 角色类型
        /// </summary>
        [Required(ErrorMessage = "角色类型不能为空")]
        [StringLength(80, ErrorMessage = "角色类型输入过长，不能超过80位")]
        [Display(Name = "角色类型")]
        public string Type { get; set; }
        /// <summary>
        /// 是否管理员角色
        /// </summary>
        [Display(Name = "是否管理员角色")]
        public bool? IsAdmin { get; set; }
        /// <summary>
        /// 是否默认角色
        /// </summary>
        [Display(Name = "是否默认角色")]
        public bool? IsDefault { get; set; }
        /// <summary>
        /// 是否系统角色
        /// </summary>
        [Display(Name = "是否系统角色")]
        public bool? IsSystem { get; set; }
        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        [Display(Name = "备注")]
        public string Remark { get; set; }
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength(50, ErrorMessage = "拼音简码输入过长，不能超过50位")]
        [Display(Name = "拼音简码")]
        public string PinYin { get; set; }
        /// <summary>
        /// 签名
        /// </summary>
        [StringLength(256, ErrorMessage = "签名输入过长，不能超过256位")]
        [Display(Name = "签名")]
        public string Sign { get; set; }
        /// <summary>
        /// 租户标识
        /// </summary>
        [StringLength(50, ErrorMessage = "租户标识输入过长，不能超过50位")]
        [Display(Name = "租户标识")]
        public string TenantId { get; set; }
        /// <summary>
        /// 是否删除
        /// </summary>
        [Display(Name = "是否删除")]
        public bool? IsDeleted { get; set; }
        /// <summary>
        /// 版本号
        /// </summary>
        [Display(Name = "版本号")]
        public Byte[] Version { get; set; }
        /// <summary>
        /// 创建时间
        /// </summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }
        /// <summary>
        /// 创建人标识
        /// </summary>
        [Display(Name = "创建人标识")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 创建人
        /// </summary>
        [StringLength(200, ErrorMessage = "创建人输入过长，不能超过200位")]
        [Display(Name = "创建人")]
        public string Creator { get; set; }
        /// <summary>
        /// 最后修改时间
        /// </summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人标识
        /// </summary>
        [Display(Name = "最后修改人标识")]
        public Guid? LastModifierId { get; set; }
        /// <summary>
        /// 最后修改人
        /// </summary>
        [StringLength(200, ErrorMessage = "最后修改人输入过长，不能超过200位")]
        [Display(Name = "最后修改人")]
        public string LastModifier { get; set; }
    }
}
