using Bing.Data.Queries.Conditions;
using System.Linq.Expressions;
using System;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// OrIfNotEmpty条件测试
/// </summary>
public class OrIfNotEmptyConditionTest
{
    /// <summary>
    /// 测试 - 全部传入null
    /// </summary>
    [Fact]
    public void Test_AllNull()
    {
        var condition = new OrIfNotEmptyCondition<Sample>(null, null);
        Assert.Null(condition.GetCondition());
    }

    /// <summary>
    /// 测试 - 条件1传入null
    /// </summary>
    [Fact]
    public void Test_PartNull_1()
    {
        Expression<Func<Sample, bool>> expression = t => t.StringValue == "a";
        var condition = new OrIfNotEmptyCondition<Sample>(null, expression);
        Assert.Equal(expression, condition.GetCondition());
    }

    /// <summary>
    /// 测试 - 条件2传入null
    /// </summary>
    [Fact]
    public void Test_PartNull_2()
    {
        Expression<Func<Sample, bool>> expression = t => t.StringValue == "a";
        var condition = new OrIfNotEmptyCondition<Sample>(expression, null);
        Assert.Equal(expression, condition.GetCondition());
    }

    /// <summary>
    /// 测试 - 三个有效条件
    /// </summary>
    [Fact]
    public void Test_Valid()
    {
        Expression<Func<Sample, bool>> expression1 = t => t.Email == "a";
        Expression<Func<Sample, bool>> expression2 = t => t.Url == "b";
        Expression<Func<Sample, bool>> expression3 = t => t.StringValue == "c";
        Expression<Func<Sample, bool>> result = t => t.Email == "a" || t.Url == "b" || t.StringValue == "c";
        var condition = new OrIfNotEmptyCondition<Sample>(expression1, expression2, expression3);
        Assert.Equal(result.ToString(), condition.GetCondition().ToString());
    }

    /// <summary>
    /// 测试 - 一个条件被忽略
    /// </summary>
    [Fact]
    public void Test_Ignore_1()
    {
        Expression<Func<Sample, bool>> expression1 = t => t.Email == "a";
        Expression<Func<Sample, bool>> expression2 = t => t.Url == "";
        var condition = new OrIfNotEmptyCondition<Sample>(expression1, expression2);
        Assert.Equal(expression1, condition.GetCondition());
    }

    /// <summary>
    /// 测试 - 一个条件被忽略
    /// </summary>
    [Fact]
    public void Test_Ignore_2()
    {
        Expression<Func<Sample, bool>> expression1 = t => t.Email == "a";
        Expression<Func<Sample, bool>> expression2 = t => t.Url == "b";
        Expression<Func<Sample, bool>> expression3 = t => t.StringValue == "";
        Expression<Func<Sample, bool>> result = t => t.Email == "a" || t.Url == "b";
        var condition = new OrIfNotEmptyCondition<Sample>(expression1, expression2, expression3);
        Assert.Equal(result.ToString(), condition.GetCondition().ToString());
    }

    /// <summary>
    /// 测试 - 无效条件
    /// </summary>
    [Fact]
    public void Test_Invalid()
    {
        Expression<Func<Sample, bool>> expression1 = t => t.StringValue == "a";
        Expression<Func<Sample, bool>> expression2 = t => t.Email == "a" && t.Url == "b";
        var condition = new OrIfNotEmptyCondition<Sample>(expression1, expression2);
        Assert.Throws<InvalidOperationException>(() => condition.GetCondition());
    }
}
