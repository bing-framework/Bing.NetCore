using System;
using System.ComponentModel.DataAnnotations;
using Bing.Auditing;
using Bing.Data;
using Bing.Domain.Entities;

namespace Bing.Admin.Commons.Domain.Models
{
    /// <summary>
    /// 地区
    /// </summary>
    [Display(Name = "地区")]
    public partial class Area : TreeEntityBase<Area>,ISoftDelete, IAuditedObjectWithName
    {
        /// <summary>
        /// 初始化一个<see cref="Area"/>类型的实例
        /// </summary>
        public Area() : this(Guid.Empty, "", 0) { }

        /// <summary>
        /// 初始化一个<see cref="Area"/>类型的实例
        /// </summary>
        /// <param name="id">地区标识</param>
        /// <param name="path">路径</param>
        /// <param name="level">级数</param>
        public Area(Guid id, string path, int level) : base(id, path, level) { }

        /// <summary>
        /// 编码
        ///</summary>
        [Display(Name = "编码")]
        [StringLength( 20, ErrorMessage = "编码输入过长，不能超过20位" )]
        public string Code { get; set; }
    
        /// <summary>
        /// 名称
        ///</summary>
        [Display(Name = "名称")]
        [Required(ErrorMessage = "名称不能为空")]
        [StringLength( 200, ErrorMessage = "名称输入过长，不能超过200位" )]
        public string Name { get; set; }
    
        /// <summary>
        /// 简称
        ///</summary>
        [Display(Name = "简称")]
        [StringLength( 200, ErrorMessage = "简称输入过长，不能超过200位" )]
        public string ShortName { get; set; }
    
        /// <summary>
        /// 行政区
        ///</summary>
        [Display(Name = "行政区")]
        public bool? AdministrativeRegion { get; set; }
    
        /// <summary>
        /// 区号
        ///</summary>
        [Display(Name = "区号")]
        [StringLength( 4, ErrorMessage = "区号输入过长，不能超过4位" )]
        public string TelCode { get; set; }
    
        /// <summary>
        /// 邮编
        ///</summary>
        [Display(Name = "邮编")]
        [StringLength( 6, ErrorMessage = "邮编输入过长，不能超过6位" )]
        public string ZipCode { get; set; }
    
        /// <summary>
        /// 经度
        ///</summary>
        [Display(Name = "经度")]
        public decimal? Longitude { get; set; }
    
        /// <summary>
        /// 纬度
        ///</summary>
        [Display(Name = "纬度")]
        public decimal? Latitude { get; set; }
    
        /// <summary>
        /// 拼音简码
        ///</summary>
        [Display(Name = "拼音简码")]
        [Required(ErrorMessage = "拼音简码不能为空")]
        [StringLength( 200, ErrorMessage = "拼音简码输入过长，不能超过200位" )]
        public string PinYin { get; set; }
    
        /// <summary>
        /// 全拼
        ///</summary>
        [Display(Name = "全拼")]
        [StringLength( 500, ErrorMessage = "全拼输入过长，不能超过500位" )]
        public string FullPinYin { get; set; }
    
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
            AddDescription( t => t.Name );
            AddDescription( t => t.ShortName );
            AddDescription( t => t.AdministrativeRegion );
            AddDescription( t => t.TelCode );
            AddDescription( t => t.ZipCode );
            AddDescription( t => t.Longitude );
            AddDescription( t => t.Latitude );
            AddDescription( t => t.ParentId );
            AddDescription( t => t.Path );
            AddDescription( t => t.Level );
            AddDescription( t => t.Enabled );
            AddDescription( t => t.SortId );
            AddDescription( t => t.PinYin );
            AddDescription( t => t.FullPinYin );
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
        protected override void AddChanges( Area other )
        {
            AddChange( t => t.Id, other.Id );
            AddChange( t => t.Code, other.Code );
            AddChange( t => t.Name, other.Name );
            AddChange( t => t.ShortName, other.ShortName );
            AddChange( t => t.AdministrativeRegion, other.AdministrativeRegion );
            AddChange( t => t.TelCode, other.TelCode );
            AddChange( t => t.ZipCode, other.ZipCode );
            AddChange( t => t.Longitude, other.Longitude );
            AddChange( t => t.Latitude, other.Latitude );
            AddChange( t => t.ParentId, other.ParentId );
            AddChange( t => t.Path, other.Path );
            AddChange( t => t.Level, other.Level );
            AddChange( t => t.Enabled, other.Enabled );
            AddChange( t => t.SortId, other.SortId );
            AddChange( t => t.PinYin, other.PinYin );
            AddChange( t => t.FullPinYin, other.FullPinYin );
            AddChange( t => t.CreationTime, other.CreationTime );
            AddChange( t => t.CreatorId, other.CreatorId );
            AddChange( t => t.Creator, other.Creator );
            AddChange( t => t.LastModificationTime, other.LastModificationTime );
            AddChange( t => t.LastModifierId, other.LastModifierId );
            AddChange( t => t.LastModifier, other.LastModifier );
        }
    }
}
