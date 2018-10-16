using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries.Trees;

namespace Bing.DbDesigner.Service.Queries.Commons {
    /// <summary>
    /// 地区查询参数
    /// </summary>
    public class AreaQuery : TreeQueryParameter {
        /// <summary>
        /// 区域标识
        /// </summary>
        [Display(Name="区域标识")]
        public Guid? AreaId { get; set; }
        
        private string _name = string.Empty;
        /// <summary>
        /// 名称
        /// </summary>
        [Display(Name="名称")]
        public string Name {
            get => _name == null ? string.Empty : _name.Trim();
            set => _name = value;
        }
        /// <summary>
        /// 行政区
        /// </summary>
        [Display(Name="行政区")]
        public bool? AdministrativeRegion { get; set; }
        
        private string _telCode = string.Empty;
        /// <summary>
        /// 区号
        /// </summary>
        [Display(Name="区号")]
        public string TelCode {
            get => _telCode == null ? string.Empty : _telCode.Trim();
            set => _telCode = value;
        }
        
        private string _zipCode = string.Empty;
        /// <summary>
        /// 邮编
        /// </summary>
        [Display(Name="邮编")]
        public string ZipCode {
            get => _zipCode == null ? string.Empty : _zipCode.Trim();
            set => _zipCode = value;
        }
        /// <summary>
        /// 经度
        /// </summary>
        [Display(Name="经度")]
        public decimal? Longitude { get; set; }
        /// <summary>
        /// 纬度
        /// </summary>
        [Display(Name="纬度")]
        public decimal? Latitude { get; set; }
        
        private string _pinYin = string.Empty;
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name="拼音简码")]
        public string PinYin {
            get => _pinYin == null ? string.Empty : _pinYin.Trim();
            set => _pinYin = value;
        }
        
        private string _fullPinYin = string.Empty;
        /// <summary>
        /// 全拼
        /// </summary>
        [Display(Name="全拼")]
        public string FullPinYin {
            get => _fullPinYin == null ? string.Empty : _fullPinYin.Trim();
            set => _fullPinYin = value;
        }
        /// <summary>
        /// 起始创建时间
        /// </summary>
        [Display( Name = "起始创建时间" )]
        public DateTime? BeginCreationTime { get; set; }
        /// <summary>
        /// 结束创建时间
        /// </summary>
        [Display( Name = "结束创建时间" )]
        public DateTime? EndCreationTime { get; set; }        
        /// <summary>
        /// 创建人
        /// </summary>
        [Display(Name="创建人")]
        public Guid? CreatorId { get; set; }
        /// <summary>
        /// 起始最后修改时间
        /// </summary>
        [Display( Name = "起始最后修改时间" )]
        public DateTime? BeginLastModificationTime { get; set; }
        /// <summary>
        /// 结束最后修改时间
        /// </summary>
        [Display( Name = "结束最后修改时间" )]
        public DateTime? EndLastModificationTime { get; set; }        
        /// <summary>
        /// 最后修改人
        /// </summary>
        [Display(Name="最后修改人")]
        public Guid? LastModifierId { get; set; }
    }
}