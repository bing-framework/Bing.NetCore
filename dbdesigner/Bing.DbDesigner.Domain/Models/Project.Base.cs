using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Bing.Domains.Entities;
using Bing.Domains.Entities.Auditing;
using Bing.Domains.Entities.Trees;

namespace Bing.DbDesigner.Domains.Models
{
    /// <summary>
    /// 项目
    /// </summary>
    [DisplayName("项目")]
    public partial class Project:AggregateRoot<Project>,IDelete,IAudited,ISortId
    {
        /// <summary>
        /// 初始化一个<see cref="Project"/>类型的实例
        /// </summary>
        public Project() : this(Guid.Empty)
        {

        }

        /// <summary>
        /// 初始化一个<see cref="Project"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        public Project(Guid id) : base(id)
        {
        }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 英文名
        /// </summary>
        public string EnglishName { get; set; }

        /// <summary>
        /// 数据状态。
        /// </summary>
        public int DataStatus { get; set; }        

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? CreationTime { get; set; }
        public Guid? CreatorId { get; set; }
        public DateTime? LastModificationTime { get; set; }
        public Guid? LastModifierId { get; set; }
        public int? SortId { get; set; }
    }
}
