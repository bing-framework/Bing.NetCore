﻿using Bing.Data.Sql.Builders.Conditions;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Data.Test.Integration.Sql.Builders.SqlServer.Conditions;

/// <summary>
/// Sql Is Not Null查询条件测试
/// </summary>
public class IsNotNullConditionTest:TestBase
{
    public IsNotNullConditionTest(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// 获取条件
    /// </summary>
    [Fact]
    public void Test_1()
    {
        var condition = new IsNotNullCondition("Email");
        Assert.Equal("Email Is Not Null", condition.GetCondition());
    }

    /// <summary>
    /// 获取条件 - 验证列为空
    /// </summary>
    [Fact]
    public void Test_2()
    {
        var condition = new IsNotNullCondition("");
        Assert.Null(condition.GetCondition());
    }
}