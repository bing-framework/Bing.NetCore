using System;
using System.Text;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Tests.Samples;
using Xunit;

namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// Sql相等查询条件测试
/// </summary>
public class EqualConditionTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public EqualConditionTest()
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
        Assert.Throws<ArgumentNullException>(() =>
        {
            var condition = new EqualSqlCondition(_parameterManager, "", 1, true);
        });
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_1()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", 1, true);
        Assert.Equal("a=@_p_0", GetResult(condition));
        Assert.Equal(1, _parameterManager.GetParamValue("@_p_0"));
    }

    /// <summary>
    /// 测试 - 获取条件 - 非参数化
    /// </summary>
    [Fact]
    public void Test_GetCondition_2()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", "b", false);
        Assert.Equal("a=b", GetResult(condition));
        Assert.Equal(0, _parameterManager.GetParams().Count);
    }

    /// <summary>
    /// 测试 - 获取条件 - 参数值为null,则输出 is null
    /// </summary>
    [Fact]
    public void Test_GetCondition_3()
    {
        var condition = new EqualSqlCondition(_parameterManager, "a", null, true);
        Assert.Equal("a Is Null", GetResult(condition));
        Assert.Equal(0, _parameterManager.GetParams().Count);
    }

    ///// <summary>
    ///// 测试 - 获取条件 - 值为ISqlBuilder
    ///// </summary>
    //[Fact]
    //public void Test_GetCondition_4()
    //{
    //    var result = new StringBuilder();
    //    result.Append("a=");
    //    result.AppendLine("(Select [a] ");
    //    result.Append("From [b])");

    //    var builder = new TestSqlBuilder().Select("a").From("b");
    //    var condition = new EqualSqlCondition(_parameterManager, "a", builder, true);
    //    Assert.Equal(result.ToString(), GetResult(condition));
    //}
}
