using Bing.Data.Queries;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// 范围过滤条件测试
/// </summary>
public class SegmentConditionTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public SegmentConditionTest()
    {
        _parameterManager = new ParameterManager(TestDialect.Instance);
    }

    /// <summary>
    /// 获取结果
    /// </summary>
    private string GetResult(ISqlCondition condition)
    {
        var result = new StringBuilder();
        condition.AppendTo(result);
        return result.ToString();
    }

    /// <summary>
    /// 测试 - 创建条件 - 验证列名为空
    /// </summary>
    [Fact]
    public void Test_Create_Validate()
    {
        Assert.Throws<ArgumentNullException>(() => {
            var condition = new SegmentSqlCondition(_parameterManager, "", 1, 2, Boundary.Left, true);
        });
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 包含两边边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_1()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, 2, Boundary.Both, true);
        Assert.Equal("a>=@_p_0 And a<=@_p_1", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 包含左边边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_2()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, 2, Boundary.Left, true);
        Assert.Equal("a>=@_p_0 And a<@_p_1", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 包含右边边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_3()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, 2, Boundary.Right, true);
        Assert.Equal("a>@_p_0 And a<=@_p_1", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 不包含边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_4()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, 2, Boundary.Neither, true);
        Assert.Equal("a>@_p_0 And a<@_p_1", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
        Assert.Equal(2, _parameterManager.GetValue("@_p_1"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 没有最大值
    /// </summary>
    [Fact]
    public void Test_GetCondition_5()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, null, Boundary.Both, true);
        Assert.Equal("a>=@_p_0", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化 - 没有最小值
    /// </summary>
    [Fact]
    public void Test_GetCondition_6()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", null, 1, Boundary.Both, true);
        Assert.Equal("a<=@_p_0", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 非参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_7()
    {
        var condition = new SegmentSqlCondition(_parameterManager, "a", 1, 2, Boundary.Both, false);
        Assert.Equal("a>=1 And a<=2", GetResult(condition));
        Assert.Equal(0, _parameterManager.GetParams().Count);
    }
}
