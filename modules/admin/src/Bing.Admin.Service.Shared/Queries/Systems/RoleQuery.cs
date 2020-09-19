using System;
using System.ComponentModel.DataAnnotations;
using Bing.Data.Queries;

namespace Bing.Admin.Service.Shared.Queries.Systems
{
    /// <summary>
    /// 角色查询参数
    /// </summary>
    public class RoleQuery : TreeQueryParameter
    {
        /// <summary>
        /// 角色标识
        /// </summary>
        [Display(Name = "角色标识")]
        public Guid? RoleId { get; set; }

        private string _code = string.Empty;
        /// <summary>
        /// 角色编码
        /// </summary>
        [Display(Name = "角色编码")]
        public string Code
        {
            get => _code == null ? string.Empty : _code.Trim();
            set => _code = value;
        }

        private string _name = string.Empty;
        /// <summary>
        /// 角色名称
        /// </summary>
        [Display(Name = "角色名称")]
        public string Name
        {
            get => _name == null ? string.Empty : _name.Trim();
            set => _name = value;
        }

        private string _normalizedName = string.Empty;
        /// <summary>
        /// 标准化角色名称
        /// </summary>
        [Display(Name = "标准化角色名称")]
        public string NormalizedName
        {
            get => _normalizedName == null ? string.Empty : _normalizedName.Trim();
            set => _normalizedName = value;
        }

        private string _type = string.Empty;
        /// <summary>
        /// 角色类型
        /// </summary>
        [Display(Name = "角色类型")]
        public string Type
        {
            get => _type == null ? string.Empty : _type.Trim();
            set => _type = value;
        }
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

        private string _remark = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        public string Remark
        {
            get => _remark == null ? string.Empty : _remark.Trim();
            set => _remark = value;
        }

        private string _pinYin = string.Empty;
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name = "拼音简码")]
        public string PinYin
        {
            get => _pinYin == null ? string.Empty : _pinYin.Trim();
            set => _pinYin = value;
        }

        private string _sign = string.Empty;
        /// <summary>
        /// 签名
        /// </summary>
        [Display(Name = "签名")]
        public string Sign
        {
            get => _sign == null ? string.Empty : _sign.Trim();
            set => _sign = value;
        }

        private string _tenantId = string.Empty;
        /// <summary>
        /// 租户标识
        /// </summary>
        [Display(Name = "租户标识")]
        public string TenantId
        {
            get => _tenantId == null ? string.Empty : _tenantId.Trim();
            set => _tenantId = value;
        }
        /// <summary>
        /// 起始创建时间
        /// </summary>
        [Display(Name = "起始创建时间")]
        public DateTime? BeginCreationTime { get; set; }
        /// <summary>
        /// 结束创建时间
        /// </summary>
        [Display(Name = "结束创建时间")]
        public DateTime? EndCreationTime { get; set; }
        /// <summary>
        /// 创建人标识
        /// </summary>
        [Display(Name = "创建人标识")]
        public Guid? CreatorId { get; set; }

        private string _creator = string.Empty;
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name = "创建人")]
        public string Creator
        {
            get => _creator == null ? string.Empty : _creator.Trim();
            set => _creator = value;
        }
        /// <summary>
        /// 起始最后修改时间
        /// </summary>
        [Display(Name = "起始最后修改时间")]
        public DateTime? BeginLastModificationTime { get; set; }
        /// <summary>
        /// 结束最后修改时间
        /// </summary>
        [Display(Name = "结束最后修改时间")]
        public DateTime? EndLastModificationTime { get; set; }
        /// <summary>
        /// 最后修改人标识
        /// </summary>
        [Display(Name = "最后修改人标识")]
        public Guid? LastModifierId { get; set; }

        private string _lastModifier = string.Empty;
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name = "最后修改人")]
        public string LastModifier
        {
            get => _lastModifier == null ? string.Empty : _lastModifier.Trim();
            set => _lastModifier = value;
        }
    }
}
