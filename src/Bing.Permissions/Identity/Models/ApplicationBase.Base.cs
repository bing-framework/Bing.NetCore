using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Domains.Entities;

namespace Bing.Permissions.Identity.Models
{
    /// <summary>
    /// 应用程序基类
    /// </summary>
    /// <typeparam name="TApplication">应用程序类型</typeparam>
    /// <typeparam name="TKey">应用程序标识类型</typeparam>
    [Display(Name = "应用程序")]
    public abstract partial class ApplicationBase<TApplication, TKey> : AggregateRoot<TApplication, TKey>, IDelete, IAuditedObject
        where TApplication : ApplicationBase<TApplication, TKey>
    {
        #region 属性

        /// <summary>
        /// 应用程序编码
        /// </summary>
        [Display(Name = "应用程序编码")]
        [Required(ErrorMessage = "应用程序编码不能为空")]
        [StringLength(50, ErrorMessage = "应用程序编码输入过长，不能超过50位")]
        public string Code { get; set; }

        /// <summary>
        /// 应用程序名称
        /// </summary>
        [Display(Name = "应用程序名称")]
        [Required(ErrorMessage = "应用程序名称不能为空")]
        [StringLength(200, ErrorMessage = "应用程序名称输入过长，不能超过200位")]
        public string Name { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        [Required(ErrorMessage = "启用不能为空")]
        public bool Enabled { get; set; }

        /// <summary>
        /// 启用注册
        /// </summary>
        [Display(Name = "启用注册")]
        [Required(ErrorMessage = "启用注册不能为空")]
        public bool RegisterEnabled { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name = "备注")]
        [StringLength(500)]
        public string Remark { get; set; }

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
        /// 初始化一个<see cref="AggregateRoot{TEntity,TKey}"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        protected ApplicationBase(TKey id) : base(id) { }

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
            AddDescription(t => t.Enabled);
            AddDescription(t => t.RegisterEnabled);
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
        protected override void AddChanges(TApplication other)
        {
            AddChange(t => t.Id, other.Id);
            AddChange(t => t.Code, other.Code);
            AddChange(t => t.Name, other.Name);
            AddChange(t => t.Enabled, other.Enabled);
            AddChange(t => t.RegisterEnabled, other.RegisterEnabled);
            AddChange(t => t.Remark, other.Remark);
            AddChange(t => t.CreationTime, other.CreationTime);
            AddChange(t => t.CreatorId, other.CreatorId);
            AddChange(t => t.LastModificationTime, other.LastModificationTime);
            AddChange(t => t.LastModifierId, other.LastModifierId);
        }

        #endregion
    }
}
