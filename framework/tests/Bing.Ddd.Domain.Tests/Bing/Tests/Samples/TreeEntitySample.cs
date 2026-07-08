using Bing.Domain.Entities;
using Bing.Trees;
using Bing.Validation;

namespace Bing.Tests.Samples;

/// <summary>
/// 树型实体测试样本（Guid 标识 + Guid? 父标识）
/// </summary>
public class TreeEntitySample : TreeEntityBase<TreeEntitySample>
{
    public TreeEntitySample() : this(Guid.NewGuid()) { }

    public TreeEntitySample(Guid id) : base(id, string.Empty, 1) { }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 验证 - 空实现，仅供测试使用
    /// </summary>
    protected override void Validate(ValidationResultCollection results)
    {
    }
}
