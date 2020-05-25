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
using Bing.Domains.Entities.Tenants;

namespace Bing.Admin.Commons.Domain.Models
{
    /// <summary>
    /// 文件
    /// </summary>
    [Display(Name = "文件")]
    public partial class File : AggregateRoot<File>,ICreationAuditedWithNameObject
    {
        /// <summary>
        /// 初始化一个<see cref="File"/>类型的实例
        /// </summary>
        public File() : this(Guid.Empty) { }

        /// <summary>
        /// 初始化一个<see cref="File"/>类型的实例
        /// </summary>
        /// <param name="id">文件标识</param>
        public File(Guid id) : base(id) { }

        /// <summary>
        /// 名称
        ///</summary>
        [Display(Name = "名称")]
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
        public string Name { get; set; }
    
        /// <summary>
        /// 大小
        ///</summary>
        [Display(Name = "大小")]
        [Required(ErrorMessage = "大小不能为空")]
        public long Size { get; set; }
    
        /// <summary>
        /// 大小说明
        ///</summary>
        [Display(Name = "大小说明")]
        [StringLength( 50, ErrorMessage = "大小说明输入过长，不能超过50位" )]
        public string SizeExplain { get; set; }
    
        /// <summary>
        /// 扩展名
        ///</summary>
        [Display(Name = "扩展名")]
        [Required(ErrorMessage = "扩展名不能为空")]
        [StringLength( 10, ErrorMessage = "扩展名输入过长，不能超过10位" )]
        public string ExtName { get; set; }
    
        /// <summary>
        /// 地址
        ///</summary>
        [Display(Name = "地址")]
        [Required(ErrorMessage = "地址不能为空")]
        public string Address { get; set; }
    
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
        /// 添加描述
        /// </summary>
        protected override void AddDescriptions()
        {
            AddDescription( t => t.Id );
            AddDescription( t => t.Name );
            AddDescription( t => t.Size );
            AddDescription( t => t.SizeExplain );
            AddDescription( t => t.ExtName );
            AddDescription( t => t.Address );
            AddDescription( t => t.CreationTime );
            AddDescription( t => t.CreatorId );
            AddDescription( t => t.Creator );
        }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges( File other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.Size, other.Size );
            AddChange( t => t.SizeExplain, other.SizeExplain );
            AddChange( t => t.ExtName, other.ExtName );
            AddChange( t => t.Address, other.Address );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
        }
    }
}