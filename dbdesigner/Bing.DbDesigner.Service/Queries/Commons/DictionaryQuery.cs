using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries.Trees;

namespace Bing.DbDesigner.Service.Queries.Commons {
    /// <summary>
    /// 字典查询参数
    /// </summary>
    public class DictionaryQuery : TreeQueryParameter {
        /// <summary>
        /// 字典标识
        /// </summary>
        [Display(Name="字典标识")]
        public Guid? DictionaryId { get; set; }
        
        private string _code = string.Empty;
        /// <summary>
        /// 编码
        /// </summary>
        [Display(Name="编码")]
        public string Code {
            get => _code == null ? string.Empty : _code.Trim();
            set => _code = value;
        }
        
        private string _text = string.Empty;
        /// <summary>
        /// 文本
        /// </summary>
        [Display(Name="文本")]
        public string Text {
            get => _text == null ? string.Empty : _text.Trim();
            set => _text = value;
        }
        
        private string _pinYin = string.Empty;
        /// <summary>
        /// 拼音简码
        /// </summary>
        [Display(Name="拼音简码")]
        public string PinYin {
            get => _pinYin == null ? string.Empty : _pinYin.Trim();
            set => _pinYin = value;
        }
        
        private string _note = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name="备注")]
        public string Note {
            get => _note == null ? string.Empty : _note.Trim();
            set => _note = value;
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
        
        private string _tenantId = string.Empty;
        /// <summary>
        /// 租户标识
        /// </summary>
        [Display(Name="租户标识")]
        public string TenantId {
            get => _tenantId == null ? string.Empty : _tenantId.Trim();
            set => _tenantId = value;
        }
    }
}