using Bing.Exceptions;
using Bing.Test.Shared;
using Bing.Tests.Samples;

namespace Bing.Domain.Entities;

/// <summary>
/// string 标识实体测试
/// </summary>
public class StringEntityTest
{
    /// <summary>
    /// 聚合根测试样例
    /// </summary>
    private StringAggregateRootSample _sample;

    /// <summary>
    /// 聚合根测试样例2
    /// </summary>
    private StringAggregateRootSample _sample2;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public StringEntityTest()
    {
        _sample = new StringAggregateRootSample();
        _sample2 = new StringAggregateRootSample();
    }

    /// <summary>
    /// 测试 - 实体相等性 - 当两个实体的标识相同，则实体相同
    /// </summary>
    [Fact]
    public void Equals_Id_Equals()
    {
        _sample = new StringAggregateRootSample("a");
        _sample2 = new StringAggregateRootSample("a");

        Assert.True(_sample.Equals(_sample2));
        Assert.True(_sample == _sample2);
        Assert.False(_sample != _sample2);
    }

    /// <summary>
    /// 测试 - 实体相等性 - Id为空
    /// </summary>
    [Fact]
    public void Equals_Id_Null()
    {
        _sample = new StringAggregateRootSample(null);
        _sample2 = new StringAggregateRootSample("a");

        Assert.False(_sample.Equals(_sample2));
        Assert.False(_sample == _sample2);
        Assert.False(_sample2 == _sample);

        _sample = new StringAggregateRootSample("a");
        _sample2 = new StringAggregateRootSample(null);

        Assert.False(_sample.Equals(_sample2));
        Assert.False(_sample == _sample2);
        Assert.False(_sample2 == _sample);
    }

    /// <summary>
    /// 测试 - 输出字符串 - 重写方法
    /// </summary>
    [Fact]
    public void ToString_Override()
    {
        _sample = new StringAggregateRootSample { Name = "a" };
        Assert.Equal($"Id:{_sample.Id},姓名:a", _sample.ToString());
    }

    /// <summary>
    /// 测试 - 验证 - Id为空，无法通过
    /// </summary>
    [Fact]
    public void Validate_Id_IsEmpty()
    {
        AssertHelper.Throws<Warning>(() => {
            _sample = new StringAggregateRootSample(null);
            _sample.Validate();
        }, "标识");

        AssertHelper.Throws<Warning>(() =>
        {
            _sample = new StringAggregateRootSample("");
            _sample.Validate();
        }, "标识");
    }
}
