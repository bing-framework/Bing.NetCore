using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Utils;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Auditing;
using Bing.Domains;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Trees;
using Bing.Domains.Entities.Tenants;

namespace Bing.Admin.Commons.Domain.Models
{
    /// <summary>
    /// 字典
    /// </summary>
    [Display(Name = "字典")]
    public partial class Dictionary : TreeEntityBase<Dictionary>,IDelete,IAuditedWithNameObject
    {
        /// <summary>
        /// 初始化一个<see cref="Dictionary"/>类型的实例
        /// </summary>
        public Dictionary() : this(Guid.Empty, "", 0) { }

        /// <summary>
        /// 初始化一个<see cref="Dictionary"/>类型的实例
        /// </summary>
        /// <param name="id">字典标识</param>
        /// <param name="path">路径</param>
        /// <param name="level">级数</param>
        public Dictionary(Guid id, string path, int level) : base(id, path, level) { }

        /// <summary>
        /// 编码
        ///</summary>
        [Display(Name = "编码")]
        [StringLength( 100, ErrorMessage = "编码输入过长，不能超过100位" )]
        public string Code { get; set; }
    
        /// <summary>
        /// 文本
        ///</summary>
        [Display(Name = "文本")]
        [Required(ErrorMessage = "文本不能为空")]
        [StringLength( 200, ErrorMessage = "文本输入过长，不能超过200位" )]
        public string Text { get; set; }
    
        /// <summary>
        /// 拼音简码
        ///</summary>
        [Display(Name = "拼音简码")]
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 50, ErrorMessage = "拼音简码输入过长，不能超过50位" )]
        public string PinYin { get; set; }
    
        /// <summary>
        /// 是否删除
        ///</summary>
        [Display(Name = "是否删除")]
        public bool IsDeleted { get; set; }
    
        /// <summary>
        /// 创建时间
        ///</summary>
        [Display(Name = "创建时间")]
        public DateTime? CreationTime { get; set; }
    
        /// <summary>
        /// 创建人编号
        ///</summary>
        [Display(Name = "创建人编号")]
        public Guid? CreatorId { get; set; }
    
        /// <summary>
        /// 创建人
        ///</summary>
        [Display(Name = "创建人")]
        [StringLength( 200, ErrorMessage = "创建人输入过长，不能超过200位" )]
        public string Creator { get; set; }
    
        /// <summary>
        /// 最后修改时间
        ///</summary>
        [Display(Name = "最后修改时间")]
        public DateTime? LastModificationTime { get; set; }
    
        /// <summary>
        /// 最后修改人编号
        ///</summary>
        [Display(Name = "最后修改人编号")]
        public Guid? LastModifierId { get; set; }
    
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
            AddDescription( t => t.Text );
            AddDescription( t => t.ParentId );
            AddDescription( t => t.Path );
            AddDescription( t => t.Level );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.SortId );
            AddDescription( t => t.PinYin );
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
        protected override void AddChanges( Dictionary other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Text, other.Text );
            AddChange( t => t.ParentId, other.ParentId );
            AddChange( t => t.Path, other.Path );
            AddChange( t => t.Level, other.Level );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.PinYin, other.PinYin );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.LastModifier, other.LastModifier );
        }
    }
}