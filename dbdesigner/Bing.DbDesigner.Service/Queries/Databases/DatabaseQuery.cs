using System;
using System.ComponentModel.DataAnnotations;
using Bing;
using Bing.Datas.Queries;

namespace Bing.DbDesigner.Service.Queries.Databases {
    /// <summary>
    /// 数据库查询参数
    /// </summary>
    public class DatabaseQuery : QueryParameter {
        /// <summary>
        /// 数据库标识
        /// </summary>
        [Display(Name="数据库标识")]
        public Guid? DatabaseId { get; set; }
        /// <summary>
        /// 用户标识
        /// </summary>
        [Display(Name="用户标识")]
        public Guid? UserId { get; set; }
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
        
        private string _addreviation = string.Empty;
        /// <summary>
        /// 缩写
        /// </summary>
        [Display(Name="缩写")]
        public string Addreviation {
            get => _addreviation == null ? string.Empty : _addreviation.Trim();
            set => _addreviation = value;
        }
        
        private string _dbType = string.Empty;
        /// <summary>
        /// 数据库类型
        /// </summary>
        [Display(Name="数据库类型")]
        public string DbType {
            get => _dbType == null ? string.Empty : _dbType.Trim();
            set => _dbType = value;
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
    }
}