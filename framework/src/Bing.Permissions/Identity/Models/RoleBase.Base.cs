using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Permissions.Identity.Models
{
    /// <summary>
    /// 角色基类
    /// </summary>
    /// <typeparam name="TRole">角色类型</typeparam>
    /// <typeparam name="TKey">角色标识类型</typeparam>
    /// <typeparam name="TParentId">角色父标识类型</typeparam>
    [Display(Name = "角色")]
    public abstract partial class RoleBase<TRole, TKey, TParentId> : TreeEntityBase<TRole, TKey, TParentId>, ISoftDelete, IAuditedObject where TRole : RoleBase<TRole, TKey, TParentId>
    {
        #region 属性

        /// <summary>
        /// 角色编码
        /// </summary>
        [Display(Name = "角色编码")]
        [Required(ErrorMessage = "角色编码不能为空")]
        [StringLength(256, ErrorMessage = "角色编码输入过长，不能超过256位")]
        public string Code { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        [Display(Name = "角色名称")]
        [Required(ErrorMessage = "角色名称不能为空")]
        [StringLength(256, ErrorMessage = "角色名称输入过长，不能超过256位")]
        public string Name { get; set; }

        /// <summary>
        /// 标准化角色名称
        /// </summary>
        [Display(Name = "标准化角色名称")]
        [Required(ErrorMessage = "标准化角色名称不能为空")]
        [StringLength(256, ErrorMessage = "标准化角色名称输入过长，不能超过256位")]
        public string NormalizedName { get; set; }

        /// <summary>
        /// 角色类型
        /// </summary>
        [Display(Name = "角色类型")]
        [Required(ErrorMessage = "角色类型不能为空")]
        [StringLength(80, ErrorMessage = "角色类型输入过长，不能超过80位")]
        public string Type { get; set; }

        /// <summary>
        /// 是否管理员角色
        /// </summary>
        [Display(Name = "是否管理员角色")]
        [Required(ErrorMessage = "是否管理员角色不能为空")]
        public bool IsAdmin { get; set; }

        /// <summary>
        /// 是否默认角色
        /// </summary>
        [Display(Name = "是否默认角色")]
        [Required(ErrorMessage = "是否默认角色不能为空")]
        public bool IsDefault { get; set; }

        /// <summary>
        /// 是否系统角色
        /// </summary>
        [Display(Name = "是否系统角色")]
        [Required(ErrorMessage = "是否系统角色不能为空")]
        public bool IsSystem { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        public string Remark { get; set; }

        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name = "拼音简码")]
        [StringLength(200, ErrorMessage = "拼音简码输入过长，不能超过200位")]
        public string PinYin { get; set; }

        /// <summary>
        /// 签名
        /// </summary>
        [Display(Name = "签名")]
        [StringLength(256, ErrorMessage = "签名输入过长，不能超过256位")]
        public string Sign { get; set; }

        /// <summary>
        /// 是否删除
        /// </summary>
        [Display(Name = "是否删除")]
        [Required(ErrorMessage = "是否删除不能为空")]
        public bool IsDeleted { get; set; }

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
        /// 最后修改时间
        /// </summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        [Display(Name = "最后修改人标识")]
        public Guid? LastModifierId { get; set; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化一个<see cref="TreeEntityBase{TEntity,TKey,TParentId}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        /// <param name="path">路径</param>
        /// <param name="level">级数</param>
        protected RoleBase(TKey id, string path, int level) : base(id, path, level) { }

        #endregion

        #region AddDescriptions(添加描述)

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription(t => t.Id);
            AddDescription(t => t.Code);
            AddDescription(t => t.Name);
            AddDescription(t => t.NormalizedName);
            AddDescription(t => t.Type);
            AddDescription(t => t.IsAdmin);
            AddDescription(t => t.ParentId);
            AddDescription(t => t.Path);
            AddDescription(t => t.Level);
            AddDescription(t => t.SortId);
            AddDescription(t => t.Enabled);
            AddDescription(t => t.Remark);
            AddDescription(t => t.PinYin);
            AddDescription(t => t.Sign);
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
        protected override void AddChanges(TRole other)
        {
            AddChange(t => t.Id, other.Id);
            AddChange(t => t.Code, other.Code);
            AddChange(t => t.Name, other.Name);
            AddChange(t => t.NormalizedName, other.NormalizedName);
            AddChange(t => t.Type, other.Type);
            AddChange(t => t.IsAdmin, other.IsAdmin);
            AddChange(t => t.ParentId, other.ParentId);
            AddChange(t => t.Path, other.Path);
            AddChange(t => t.Level, other.Level);
            AddChange(t => t.SortId, other.SortId);
            AddChange(t => t.Enabled, other.Enabled);
            AddChange(t => t.Remark, other.Remark);
            AddChange(t => t.PinYin, other.PinYin);
            AddChange(t => t.Sign, other.Sign);
            AddChange(t => t.CreationTime, other.CreationTime);
            AddChange(t => t.CreatorId, other.CreatorId);
            AddChange(t => t.LastModificationTime, other.LastModificationTime);
            AddChange(t => t.LastModifierId, other.LastModifierId);
        }

        #endregion
    }
}
