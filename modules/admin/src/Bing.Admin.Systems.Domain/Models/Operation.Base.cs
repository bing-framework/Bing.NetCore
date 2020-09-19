using System;
using System.ComponentModel.DataAnnotations;
using Bing.Domain.Entities;

namespace Bing.Admin.Systems.Domain.Models
{
    /// <summary>
    /// 操作
    /// </summary>
    [Display(Name = "操作")]
    public partial class Operation : AggregateRoot<Operation>
    {
        /// <summary>
        /// 初始化一个<see cref="Operation" />类型的实例
        /// </summary>
        public Operation() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="Operation" />类型的实例
        /// </summary>
        /// <param name="id">操作标识</param>
        public Operation(Guid id) : base(id)
        {
        }

        /// <summary>
        /// 应用程序标识
        /// </summary>
        public Guid? ApplicationId { get; set; }

        /// <summary>
        /// 模块标识
        /// </summary>
        [Required(ErrorMessage = "模块标识不能为空")]
        public Guid ModuleId { get; set; }

        /// <summary>
        /// 操作编码
        /// </summary>
        [Display(Name = "操作编码")]
        [Required(ErrorMessage = "操作编码不能为空")]
        [StringLength(300, ErrorMessage = "编码输入过长，不能超过300位")]
        public string Code { get; set; }

        /// <summary>
        /// 操作名称
        /// </summary>
        [Required(ErrorMessage = "操作名称不能为空")]
        [StringLength(200, ErrorMessage = "操作名称输入过长，不能超过200位")]
        public string Name { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        [StringLength(500, ErrorMessage = "备注输入过长，不能超过500位")]
        public string Remark { get; set; }

        /// <summary>
        /// 拼音简码
        /// </summary>
        [StringLength(200, ErrorMessage = "拼音简码输入过长，不能超过200位")]
        public string PinYin { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name = "启用")]
        public bool? Enabled { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int? SortId { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public Guid? CreatorId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        public Guid? LastModifierId { get; set; }

        /// <summary>
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription(t => t.Id);
            AddDescription(t => t.ApplicationId);
            AddDescription(t => t.ModuleId);
            AddDescription(t => t.Code);
            AddDescription(t => t.Name);
            AddDescription(t => t.Icon);
            AddDescription(t => t.Remark);
            AddDescription(t => t.Enabled);
            AddDescription(t => t.SortId);
            AddDescription(t => t.PinYin);
            AddDescription(t => t.CreationTime);
            AddDescription(t => t.CreatorId);
            AddDescription(t => t.LastModificationTime);
            AddDescription(t => t.LastModifierId);
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges(Operation other)
        {
            AddChange(t => t.Id, other.Id);
            AddChange(t => t.ApplicationId, other.ApplicationId);
            AddChange(t => t.ModuleId, other.ModuleId);
            AddChange(t => t.Code, other.Code);
            AddChange(t => t.Name, other.Name);
            AddChange(t => t.Remark, other.Remark);
            AddChange(t => t.Enabled, other.Enabled);
            AddChange(t => t.SortId, other.SortId);
            AddChange(t => t.PinYin, other.PinYin);
            AddChange(t => t.CreationTime, other.CreationTime);
            AddChange(t => t.CreatorId, other.CreatorId);
            AddChange(t => t.LastModificationTime, other.LastModificationTime);
            AddChange(t => t.LastModifierId, other.LastModifierId);
        }
    }
}
