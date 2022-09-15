using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoMapper;
using Bing.Application.Dtos;
using Bing.Auditing;
using Bing.Domain.Entities;

namespace Bing.SampleClasses
{
    /// <summary>
    /// 实体样例
    /// </summary>
    [Display(Name = "实体样例")]
    public class EntitySample : AggregateRoot<EntitySample>, IAuditedObjectWithName
    {
        /// <summary>
        /// 初始化一个<see cref="EntitySample"/>类型的实例
        /// </summary>
        public EntitySample() : this(Guid.NewGuid()) { }

        /// <summary>
        /// 初始化一个<see cref="EntitySample"/>类型的实例
        /// </summary>
        /// <param name="id">标识</param>
        public EntitySample(Guid id) : base(id) { }

        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 忽略值
        /// </summary>
        [IgnoreMap]
        public string IgnoreValue { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 创建人标识
        /// </summary>
        public Guid? CreatorId { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public Guid? LastModifierId { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        public string LastModifier { get; set; }

        /// <summary>
        /// 添加变更列表
        /// </summary>
        protected override void AddChanges(EntitySample other)
        {
            AddChange(x => x.Id, other.Id);
            AddChange(x => x.Name, other.Name);
            AddChange(x => x.LastModificationTime, other.LastModificationTime);
        }
    }

    /// <summary>
    /// 数据传输对象样例
    /// </summary>
    public class DtoSample : DtoBase, IAuditedObjectWithName
    {
        /// <summary>
        /// 名称
        /// </summary>
        [Required(ErrorMessage = "名称不能为空")]
        public string Name { get; set; }

        /// <summary>
        /// 忽略值
        /// </summary>
        [IgnoreMap]
        public string IgnoreValue { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreationTime { get; set; }

        /// <summary>
        /// 创建人标识
        /// </summary>
        public Guid? CreatorId { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModificationTime { get; set; }

        /// <summary>
        /// 最后修改人标识
        /// </summary>
        public Guid? LastModifierId { get; set; }

        /// <summary>
        /// 最后修改人
        /// </summary>
        public string LastModifier { get; set; }

        /// <summary>
        /// 创建空集合
        /// </summary>
        public static List<DtoSample> EmptyList() => new();
    }
}
