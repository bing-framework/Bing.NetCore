namespace Bing.Data.Sql.Tests.Builders.Conditions;

/// <summary>
/// And连接条件测试
/// </summary>
public class AndConditionTest
{
    /// <summary>
    /// 参数管理器
    /// </summary>
    private readonly IParameterManager _parameterManager;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public AndConditionTest()
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
    /// 测试 - 与连接条件
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var result = new StringBuilder();
        var condition1 = new EqualSqlCondition(_parameterManager, "Name", "@Name", false);
        condition1.AppendTo(result);
        var condition2 = new GreaterSqlCondition(_parameterManager, "Age", "@Age", false);
        var andCondition = new AndSqlCondition(condition2);
        andCondition.AppendTo(result);
        Assert.Equal("Name=@Name And Age>@Age", result.ToString());
    }

    /// <summary>
    /// 测试 - 或连接条件 - 构造器传入2个条件
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var result = new StringBuilder();
        var condition1 = new EqualSqlCondition(_parameterManager, "Name", "@Name", false);
        condition1.AppendTo(result);
        var condition2 = new EqualSqlCondition(_parameterManager, "Code", "@Code", false);
        var condition3 = new GreaterSqlCondition(_parameterManager, "Age", "@Age", false);
        var orCondition = new AndSqlCondition(condition2, condition3);
        orCondition.AppendTo(result);
        Assert.Equal("Name=@Name And Code=@Code And Age>@Age", result.ToString());
    }

    /// <summary>
    /// 测试 - 或连接条件 - 第2个条件为空条件
    /// </summary>
    [Fact]
    public void Test_3()
    {
        var result = new StringBuilder();
        var condition1 = new EqualSqlCondition(_parameterManager, "Name", "@Name", false);
        condition1.AppendTo(result);
        var condition2 = new EqualSqlCondition(_parameterManager, "Code", "@Code", false);
        var orCondition = new AndSqlCondition(condition2, NullSqlCondition.Instance);
        orCondition.AppendTo(result);
        Assert.Equal("Name=@Name And Code=@Code", result.ToString());
    }

    /// <summary>
    /// 测试 - 条件1为空
    /// </summary>
    [Fact]
    public void Test_4()
    {
        var result = new StringBuilder();
        var condition2 = new GreaterSqlCondition(_parameterManager, "Age", "@Age", false);
        var andCondition = new AndSqlCondition(condition2);
        andCondition.AppendTo(result);
        Assert.Equal("Age>@Age", result.ToString());
    }

    /// <summary>
    /// 测试 - 条件2为空
    /// </summary>
    [Fact]
    public void Test_5()
    {
        var result = new StringBuilder();
        var condition1 = new EqualSqlCondition(_parameterManager, "Name", "@Name", false);
        condition1.AppendTo(result);
        var andCondition = new AndSqlCondition(null);
        andCondition.AppendTo(result);
        Assert.Equal("Name=@Name", result.ToString());
    }

    /// <summary>
    /// 测试 - 条件都为空
    /// </summary>
    [Fact]
    public void Test_6()
    {
        var result = new StringBuilder();
        var andCondition = new AndSqlCondition(null);
        andCondition.AppendTo(result);
        Assert.Empty(result.ToString());
    }
}
