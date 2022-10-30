using Bing.Data.Queries;
using Xunit;

namespace Bing.Tests.Datas.Queries;

/// <summary>
/// 测试排序生成器
/// </summary>
public class OrderByBuilderTest
{
    /// <summary>
    /// 排序生成器
    /// </summary>
    private readonly OrderByBuilder _builder;

    /// <summary>
    /// 初始化一个<see cref="OrderByBuilderTest"/>类型的实例
    /// </summary>
    public OrderByBuilderTest() => _builder = new OrderByBuilder();

    /// <summary>
    /// 测试 - 生成排序
    /// </summary>
    [Fact]
    public void Test_Generate()
    {
        Assert.Equal("", _builder.Generate());
        _builder.Add("");
        _builder.Add("A");
        _builder.Add("B", true);
        _builder.Add("C.D", true);
        Assert.Equal("A,B desc,C.D desc", _builder.Generate());
    }
}