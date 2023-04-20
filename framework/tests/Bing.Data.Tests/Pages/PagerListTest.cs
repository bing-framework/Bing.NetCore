using Bing.Data.Tests.Samples;
using Bing.Utils.Json;
using Xunit;

namespace Bing.Data.Tests.Pages;

/// <summary>
/// 分页集合测试
/// </summary>
public class PagerListTest
{
    /// <summary>
    /// 分页集合
    /// </summary>
    private readonly PagerList<Sample> _list;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public PagerListTest()
    {
        _list = new PagerList<Sample>(1, 2, 3, "Name");
        _list.Add(Sample.Create1());
        _list.Add(Sample.Create2());
    }

    /// <summary>
    /// 测试 - 集合
    /// </summary>
    [Fact]
    public void Test_List()
    {
        Assert.Equal(2, _list.Data.Count);
        Assert.Equal("B", _list[1].StringValue);
    }

    /// <summary>
    /// 测试 - 转换类型
    /// </summary>
    [Fact]
    public void Test_Convert()
    {
        var result = _list.Convert(t => new Sample());
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
