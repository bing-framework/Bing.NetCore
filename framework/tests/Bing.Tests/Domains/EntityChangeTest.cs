using System.Linq;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.Tests.Domains;

/// <summary>
/// 测试获取变更属性值 - 实体
/// </summary>
public class EntityChangeTest
{
    /// <summary>
    /// 聚合根测试样例
    /// </summary>
    private readonly AggregateRootSample _sample;

    /// <summary>
    /// 聚合根测试样例2
    /// </summary>
    private readonly AggregateRootSample _sample2;

    /// <summary>
    /// 初始化一个<see cref="EntityChangeTest"/>类型的实例
    /// </summary>
    public EntityChangeTest()
    {
        _sample = new AggregateRootSample();
        _sample.StringSample = new StringAggregateRootSample();
        _sample2 = new AggregateRootSample();
        _sample2.StringSample = new StringAggregateRootSample();
    }

    /// <summary>
    /// 测试 - 获取变更属性集
    /// </summary>
    [Fact]
    public void Test_GetChanges()
    {
        var changes = _sample.GetChanges(_sample2);
        Assert.Empty(changes);

        _sample2.Name = "a";
        changes = _sample.GetChanges(_sample2);
        Assert.Single(changes);
        Assert.Equal("", changes.First().OldValue);
        Assert.Equal("a", changes.First().NewValue);

        _sample.Name = "a";
        changes = _sample.GetChanges(_sample2);
        Assert.Empty(changes);
    }

    /// <summary>
    /// 测试 - 获取变更属性集 - 通过Lambda表达式获取显示名称
    /// </summary>
    [Fact]
    public void Test_GetChanges_Lambda_Display()
    {
        _sample2.MobilePhone = "a";
        var changes = _sample.GetChanges(_sample2).ToList();
        Assert.Single(changes);
        Assert.Equal("MobilePhone", changes[0].PropertyName);
        Assert.Equal("手机号", changes[0].Description);
        Assert.Equal("", changes[0].OldValue);
        Assert.Equal("a", changes[0].NewValue);
    }

    /// <summary>
    /// 测试 - 获取变更属性集 - 通过Lambda表达式获取描述
    /// </summary>
    [Fact]
    public void Test_GetChanges_Lambda_Description()
    {
        _sample2.Tel = 1;
        var changes = _sample.GetChanges(_sample2).ToList();
        Assert.Single(changes);
        Assert.Equal("Tel", changes[0].PropertyName);
        Assert.Equal("电话", changes[0].Description);
        Assert.Equal("0", changes[0].OldValue);
        Assert.Equal("1", changes[0].NewValue);
    }

    /// <summary>
    /// 测试 - 获取变更属性集 - 导航属性
    /// </summary>
    [Fact]
    public void Test_GetChanges_Navigate()
    {
        _sample2.Name = "a";
        _sample2.StringSample.Name = "b";
        var changes = _sample.GetChanges(_sample2).ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal("姓名", changes[0].Description);
        Assert.Equal("", changes[0].OldValue);
        Assert.Equal("a", changes[0].NewValue);
        Assert.Equal("StringSampleName", changes[1].Description);
        Assert.Equal("", changes[1].OldValue);
        Assert.Equal("b", changes[1].NewValue);
    }

    /// <summary>
    /// 测试 - 获取变更属性集 - 导航属性集合
    /// </summary>
    [Fact]
    public void Test_GetChanges_NavigateCollection()
    {
        _sample.IntSamples.Add(new IntAggregateRootSample { Name = "a" });
        _sample.IntSamples.Add(new IntAggregateRootSample() { Name = "b" });
        _sample2.IntSamples.Add(new IntAggregateRootSample() { Name = "a2" });
        _sample2.IntSamples.Add(new IntAggregateRootSample() { Name = "b2" });
        var changes = _sample.GetChanges(_sample2).ToList();
        Assert.Equal(2, changes.Count);
        Assert.Equal("a", changes[0].OldValue);
        Assert.Equal("a2", changes[0].NewValue);
        Assert.Equal("b", changes[1].OldValue);
        Assert.Equal("b2", changes[1].NewValue);
    }
}