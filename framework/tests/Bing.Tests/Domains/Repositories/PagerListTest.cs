using Bing.Data;
using Bing.Tests.Samples;
using Bing.Utils.Json;
using Xunit;

namespace Bing.Tests.Domains.Repositories;

/// <summary>
/// 分页集合测试
/// </summary>
public class PagerListTest
{
    /// <summary>
    /// 分页集合
    /// </summary>
    private readonly PagerList<AggregateRootSample> _list;

    /// <summary>
    /// 初始化一个<see cref="PagerListTest"/>类型的实例
    /// </summary>
    public PagerListTest()
    {
        _list = new PagerList<AggregateRootSample>(1, 2, 3, "Name");
        _list.Add(AggregateRootSample.CreateSample());
        _list.Add(AggregateRootSample.CreateSample2());
    }

    /// <summary>
    /// 测试 - 集合
    /// </summary>
    [Fact]
    public void Test_List()
    {
        Assert.Equal(2, _list.Data.Count);
        Assert.Equal("TestName2", _list[1].Name);
    }

    /// <summary>
    /// 测试 - 转换类型
    /// </summary>
    [Fact]
    public void Test_Convert()
    {
        var result = _list.Convert(t => new AggregateRootSample());
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.PageCount);
        Assert.Equal("Name", result.Order);
    }

    /// <summary>
    /// 测试 - json序列化
    /// </summary>
    [Fact]
    public void Test_ToJson()
    {
        var list = new PagerList<Sample>();
        list.Add(new Sample());
        Assert.Contains("PageCount", JsonHelper.ToJson(list));
    }
}