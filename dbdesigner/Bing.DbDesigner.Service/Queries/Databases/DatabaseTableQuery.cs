using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Databases {
    /// <summary>
    /// 数据表查询参数
    /// </summary>
    public class DatabaseTableQuery : QueryParameter {
        /// <summary>
        /// 数据表标识
        /// </summary>
        [Display(Name="数据表标识")]
        public Guid? DatabaseTableId { get; set; }
        /// <summary>
        /// 解决方案标识
        /// </summary>
        [Display(Name="解决方案标识")]
        public Guid? SolutionId { get; set; }
        /// <summary>
        /// 项目标识
        /// </summary>
        [Display(Name="项目标识")]
        public Guid? ProjectId { get; set; }
        /// <summary>
        /// 数据库标识
        /// </summary>
        [Display(Name="数据库标识")]
        public Guid? DatabaseId { get; set; }
        /// <summary>
        /// 数据模式标识
        /// </summary>
        [Display(Name="数据模式标识")]
        public Guid? DatabaseSchemaId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
        
        private string _code = string.Empty;
        /// <summary>
        /// 编码
        /// </summary>
        [Display(Name="编码")]
        public string Code {
            get => _code == null ? string.Empty : _code.Trim();
            set => _code = value;
        }
        
        private string _name = string.Empty;
        /// <summary>
        /// 名称
        /// </summary>
        [Display(Name="名称")]
        public string Name {
            get => _name == null ? string.Empty : _name.Trim();
            set => _name = value;
        }
        
        private string _comment = string.Empty;
        /// <summary>
        /// 注释
        /// </summary>
        [Display(Name="注释")]
        public string Comment {
            get => _comment == null ? string.Empty : _comment.Trim();
            set => _comment = value;
        }
        /// <summary>
        /// 启用
        /// </summary>
        [Display(Name="启用")]
        public bool? Enabled { get; set; }
        /// <summary>
        /// 排序号
        /// </summary>
        [Display(Name="排序号")]
        public int? SortId { get; set; }
        
        private string _note = string.Empty;
        /// <summary>
        /// 备注
        /// </summary>
        [Display(Name="备注")]
        public string Note {
            get => _note == null ? string.Empty : _note.Trim();
            set => _note = value;
        }
        
        private string _extend = string.Empty;
        /// <summary>
        /// 扩展
        /// </summary>
        [Display(Name="扩展")]
        public string Extend {
            get => _extend == null ? string.Empty : _extend.Trim();
            set => _extend = value;
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