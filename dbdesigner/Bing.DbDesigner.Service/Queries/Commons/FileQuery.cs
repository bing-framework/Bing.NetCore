using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Commons {
    /// <summary>
    /// 文件查询参数
    /// </summary>
    public class FileQuery : QueryParameter {
        /// <summary>
        /// 文件标识
        /// </summary>
        [Display(Name="文件标识")]
        public Guid? FileId { get; set; }
        
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
        /// 大小
        /// </summary>
        [Display(Name="大小")]
        public long? Size { get; set; }
        
        private string _sizeExplain = string.Empty;
        /// <summary>
        /// 大小说明
        /// </summary>
        [Display(Name="大小说明")]
        public string SizeExplain {
            get => _sizeExplain == null ? string.Empty : _sizeExplain.Trim();
            set => _sizeExplain = value;
        }
        
        private string _extensions = string.Empty;
        /// <summary>
        /// 扩展名
        /// </summary>
        [Display(Name="扩展名")]
        public string Extensions {
            get => _extensions == null ? string.Empty : _extensions.Trim();
            set => _extensions = value;
        }
        
        private string _address = string.Empty;
        /// <summary>
        /// 地址
        /// </summary>
        [Display(Name="地址")]
        public string Address {
            get => _address == null ? string.Empty : _address.Trim();
            set => _address = value;
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
    }
}