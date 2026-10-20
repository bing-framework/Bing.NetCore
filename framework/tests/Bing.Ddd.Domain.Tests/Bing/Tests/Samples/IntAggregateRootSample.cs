using Bing.Domain.Entities;

namespace Bing.Tests.Samples;

/// <summary>
/// int聚合根测试样例
/// </summary>
public class IntAggregateRootSample : AggregateRoot<IntAggregateRootSample, int>
{
    /// <summary>
    /// 初始化一个 <see cref="IntAggregateRootSample"/> 类型的实例。
    /// </summary>
    public IntAggregateRootSample() : this(0) { }

    /// <summary>
    /// 初始化一个 <see cref="IntAggregateRootSample"/> 类型的实例。
    /// </summary>
    /// <param name="id">标识</param>
    public IntAggregateRootSample(int id) : base(id) { }

    /// <summary>
    /// 获取或设置姓名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置编码
    /// </summary>
    public string Code { get; set; }

    /// <inheritdoc />
    protected override void AddDescriptions()
    {
        AddDescription($"{nameof(Id)}:{Id},");
        AddDescription("姓名", Name);
    }

    /// <inheritdoc />
    protected override void AddChanges(IntAggregateRootSample newObj)
    {
        AddChange(nameof(Name), "IntSampleName", Name, newObj.Name);
    }
}