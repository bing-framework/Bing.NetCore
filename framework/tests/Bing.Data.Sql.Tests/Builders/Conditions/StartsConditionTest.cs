namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// Sql头匹配查询条件测试
/// </summary>
public class StartsConditionTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public StartsConditionTest()
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
            var condition = new StartsSqlCondition(_parameterManager, "", 1, true);
        });
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_1()
    {
        var condition = new StartsSqlCondition(_parameterManager, "a", "b", true);
        Assert.Equal("a Like @_p_0", GetResult(condition));
        Assert.Equal("b%", _parameterManager.GetValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 非参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_2()
    {
        var condition = new StartsSqlCondition(_parameterManager, "a", "b", false);
        Assert.Equal("a Like 'b%'", GetResult(condition));
        Assert.Equal(0, _parameterManager.GetParams().Count);
    }
}
