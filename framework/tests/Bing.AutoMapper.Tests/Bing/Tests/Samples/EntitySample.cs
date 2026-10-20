using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.Configuration.Annotations;
using Bing.Application.Dtos;
using Bing.Auditing;
using Bing.Domain.Entities;

namespace Bing.Tests.Samples;

/// <summary>
/// 实体样例
/// </summary>
[Display(Name = "实体样例")]
public class EntitySample : AggregateRoot<EntitySample>, IAuditedObjectWithName
{
    /// <summary>
    /// 初始化一个 <see cref="EntitySample"/> 类型的实例。
    /// </summary>
    public EntitySample() : this(Guid.NewGuid()) { }

    /// <summary>
    /// 初始化一个 <see cref="EntitySample"/> 类型的实例。
    /// </summary>
    /// <param name="id">标识</param>
    public EntitySample(Guid id) : base(id) { }

    /// <summary>
    /// 获取或设置名称
    /// </summary>
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置忽略值
    /// </summary>
    [Ignore]
    public string IgnoreValue { get; set; }

    /// <inheritdoc />
    public DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    public Guid? CreatorId { get; set; }

    /// <inheritdoc />
    public string Creator { get; set; }

    /// <inheritdoc />
    public DateTime? LastModificationTime { get; set; }

    /// <inheritdoc />
    public Guid? LastModifierId { get; set; }

    /// <inheritdoc />
    public string LastModifier { get; set; }

    /// <inheritdoc />
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
    /// 获取或设置名称
    /// </summary>
    [Required(ErrorMessage = "名称不能为空")]
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置忽略值
    /// </summary>
    [Ignore]
    public string IgnoreValue { get; set; }

    /// <inheritdoc />
    public DateTime? CreationTime { get; set; }

    /// <inheritdoc />
    public Guid? CreatorId { get; set; }

    /// <inheritdoc />
    public string Creator { get; set; }

    /// <inheritdoc />
    public DateTime? LastModificationTime { get; set; }

    /// <inheritdoc />
    public Guid? LastModifierId { get; set; }

    /// <inheritdoc />
    public string LastModifier { get; set; }

    /// <summary>
    /// 创建空集合
    /// </summary>
    /// <returns>不包含元素的 DTO 集合。</returns>
    public static List<DtoSample> EmptyList() => new();
}
