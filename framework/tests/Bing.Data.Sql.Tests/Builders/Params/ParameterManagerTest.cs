namespace Bing.Data.Sql.Tests.Builders.Params;

/// <summary>
/// Sql参数管理器测试
/// </summary>
public class ParameterManagerTest
{
    #region 测试初始化

    /// <summary>
    /// Sql参数管理器
    /// </summary>
    private readonly ParameterManager _manager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ParameterManagerTest()
    {
        _manager = new ParameterManager(TestDialect.Instance);
    }

    #endregion

    /// <summary>
    /// 测试 - 创建参数名
    /// </summary>
    [Fact]
    public void Test_GenerateName()
    {
        Assert.Equal("@_p_0", _manager.GenerateName());
        Assert.Equal("@_p_1", _manager.GenerateName());
    }

    /// <summary>
    /// 测试 - 是否包含参数
    /// </summary>
    [Fact]
    public void Test_Contains_1()
    {
        _manager.Add("a", 1);
        Assert.True(_manager.Contains("a"));
        Assert.True(_manager.Contains("@a"));
        Assert.False(_manager.Contains("b"));
    }

    /// <summary>
    /// 测试 - 是否包含参数
    /// </summary>
    [Fact]
    public void Test_Contains_2()
    {
        _manager.Add("@a", 1);
        Assert.True(_manager.Contains("a"));
        Assert.True(_manager.Contains("@a"));
        Assert.False(_manager.Contains("b"));
    }

    /// <summary>
    /// 测试 - 获取参数列表
    /// </summary>
    [Fact]
    public void Test_GetParams()
    {
        var parameters = _manager.GetParams();
        Assert.Equal(0, parameters.Count);
    }

    /// <summary>
    /// 测试 - 添加1个参数
    /// </summary>
    [Fact]
    public void Test_Add_1()
    {
        _manager.Add("a", 1);
        var parameters = _manager.GetParams();
        Assert.Equal(1, parameters.Count);
        Assert.Equal(1, _manager.GetValue("a"));
    }

    /// <summary>
    /// 测试 - 添加2个参数
    /// </summary>
    [Fact]
    public void Test_Add_2()
    {
        _manager.Add("a", 1);
        _manager.Add("b", 2);
        var parameters = _manager.GetParams();
        Assert.Equal(2, parameters.Count);
        Assert.Equal(1, _manager.GetValue("a"));
        Assert.Equal(2, _manager.GetValue("b"));
    }

    /// <summary>
    /// 测试 - 覆盖参数
    /// </summary>
    [Fact]
    public void Test_Add_3()
    {
        _manager.Add("a", 1);
        _manager.Add("a", 2);
        var parameters = _manager.GetParams();
        Assert.Equal(1, parameters.Count);
        Assert.Equal(2, _manager.GetValue("a"));
    }

    /// <summary>
    /// 测试 - 添加参数 - 参数名为空
    /// </summary>
    [Fact]
    public void Test_Add_4()
    {
        _manager.Add("", 1);
        var parameters = _manager.GetParams();
        Assert.Equal(0, parameters.Count);
    }

    /// <summary>
    /// 测试 - 清空参数
    /// </summary>
    [Fact]
    public void Test_Clear()
    {
        var paramName = _manager.GenerateName();
        _manager.Add(paramName, 1);
        _manager.Clear();
        var parameters = _manager.GetParams();
        Assert.Equal(0, parameters.Count);
        Assert.Equal("@_p_0", _manager.GenerateName());
    }

    /// <summary>
    /// 测试 - 复制Sql参数管理器副本 - 参数
    /// </summary>
    [Fact]
    public void Test_Clone_Param()
    {
        _manager.Add("name", "a");
        var clone = _manager.Clone();
        Assert.Equal("a", clone.GetValue("name"));
    }
}
