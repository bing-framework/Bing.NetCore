using Bing.Exceptions;
using Bing.Test.Shared;
using Bing.Tests.Samples;

namespace Bing.Domain.Entities;

/// <summary>
/// Guid标识实体测试
/// </summary>
public class GuidEntityTest
{
    /// <summary>
    /// 聚合根测试样例
    /// </summary>
    private AggregateRootSample _sample;

    /// <summary>
    /// 聚合根测试样例2
    /// </summary>
    private AggregateRootSample _sample2;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public GuidEntityTest()
    {
        _sample = new AggregateRootSample();
        _sample2 = new AggregateRootSample();
    }

    /// <summary>
    /// 测试 - 实体相等性 - 是否为空
    /// </summary>
    [Fact]
    public void Equals_Null()
    {
        Assert.False(_sample.Equals(_sample2));
        Assert.False(_sample.Equals(null));

        Assert.False(_sample == _sample2);
        Assert.False(_sample == null);
        Assert.False(null == _sample2);

        Assert.True(_sample != _sample2);
        Assert.True(_sample != null);
        Assert.True(null != _sample2);

        _sample2 = null;
        Assert.False(_sample.Equals(_sample2));

        _sample = null;
        Assert.True(_sample == _sample2);
        Assert.True(_sample2 == _sample);
    }

    /// <summary>
    /// 测试 - 实体相等性 - 类型无效
    /// </summary>
    [Fact]
    public void Equals_InvalidType()
    {
        var id = Guid.NewGuid();
        _sample = new AggregateRootSample(id);
        var sample2 = new AggregateRootSample2(id);
        Assert.False(_sample.Equals(sample2));
        Assert.True(_sample != sample2);
        Assert.True(sample2 != _sample);
    }

    /// <summary>
    /// 测试 - 实体相等性 - 当两个实体的标识相同，则实体相同
    /// </summary>
    [Fact]
    public void Equals_Id_Equals()
    {
        var id = Guid.NewGuid();
        _sample = new AggregateRootSample(id);
        _sample2 = new AggregateRootSample(id);
        Assert.True(_sample.Equals(_sample2));
        Assert.True(_sample == _sample2);
        Assert.False(_sample != _sample2);
    }

    /// <summary>
    /// 测试 - 输出字符串 - 重写方法
    /// </summary>
    [Fact]
    public void ToString_Override()
    {
        _sample = new AggregateRootSample { Name = "a" };
        Assert.Equal($"Id:{_sample.Id},姓名:{_sample.Name}", _sample.ToString());
    }

    /// <summary>
    /// 测试 - 验证 - Id为空，无法通过
    /// </summary>
    [Fact]
    public void Validate_Id_IsEmpty()
    {
        AssertHelper.Throws<Warning>(() =>
        {
            _sample = AggregateRootSample.CreateSample(Guid.Empty);
            _sample.Validate();
        }, "标识");
    }

    /// <summary>
    /// 测试 - 验证 - 必填项，通过字符串设置错误消息
    /// </summary>
    [Fact]
    public void Validate_Required()
    {
        AssertHelper.Throws<Warning>(() =>
        {
            _sample.Name = null;
            _sample.Validate();
        }, "姓名不能为空");
    }

    /// <summary>
    /// 测试 - 添加验证策略
    /// </summary>
    [Fact]
    public void Validate_AddValidateStrategy()
    {
        _sample = AggregateRootSample.CreateSample();
        AssertHelper.Throws<Warning>(() =>
        {
            _sample.Name = "abcd";
            _sample.UseStrategy(new ValidateStrategySample());
            _sample.Validate();
        }, "名称长度不能大于3");
    }

    /// <summary>
    /// 测试 - 设置验证处理器 - 不进行任何操作，所以不会抛出异常
    /// </summary>
    [Fact]
    public void Validate_SetValidationHandler()
    {
        _sample = AggregateRootSample.CreateSample();
        _sample.Name = "abcd";
        _sample.UseStrategy(new ValidateStrategySample());
        _sample.SetValidationCallback(new ValidationHandlerSample());
        _sample.Validate();
    }
}
