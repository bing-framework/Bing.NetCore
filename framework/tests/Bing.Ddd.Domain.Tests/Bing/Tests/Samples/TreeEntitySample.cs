using Bing.Domain.Entities;
using Bing.Trees;
using Bing.Validation;

namespace Bing.Tests.Samples;

/// <summary>
/// 树型实体测试样本（Guid 标识 + Guid? 父标识）
/// </summary>
public class TreeEntitySample : TreeEntityBase<TreeEntitySample>
{
    /// <summary>
    /// 初始化一个 <see cref="TreeEntitySample"/> 类型的实例。
    /// </summary>
    public TreeEntitySample() : this(Guid.NewGuid()) { }

    /// <summary>
    /// 初始化一个 <see cref="TreeEntitySample"/> 类型的实例。
    /// </summary>
    /// <param name="id">实体标识。</param>
    public TreeEntitySample(Guid id) : base(id, string.Empty, 1) { }

    /// <summary>
    /// 获取或设置名称
    /// </summary>
    public string Name { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// 验证 - 空实现，仅供测试使用
    /// </remarks>
    protected override void Validate(ValidationResultCollection results)
    {
    }
}
