﻿using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// 测试decimal范围过滤条件
/// </summary>
public class DecimalSegmentConditionTest
{
    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetCondition()
    {
        var condition = new DecimalSegmentCondition<Sample, decimal>(t => t.DecimalValue, 1.1M, 10.1M);
        Assert.Equal("t => ((t.DecimalValue >= 1.1) AndAlso (t.DecimalValue <= 10.1))", condition.GetCondition().ToString());

        var criteria2 = new DecimalSegmentCondition<Sample, decimal?>(t => t.NullableDecimalValue, 1.1M, 10.1M);
        Assert.Equal("t => ((t.NullableDecimalValue >= 1.1) AndAlso (t.NullableDecimalValue <= 10.1))", criteria2.GetCondition().ToString());
    }

    /// <summary>
    /// 测试 - 获取查询条件 - 设置边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_Boundary()
    {
        var condition = new DecimalSegmentCondition<Sample, decimal>(t => t.DecimalValue, 1.1M, 10.1M, Boundary.Neither);
        Assert.Equal("t => ((t.DecimalValue > 1.1) AndAlso (t.DecimalValue < 10.1))", condition.GetCondition().ToString());

        condition = new DecimalSegmentCondition<Sample, decimal>(t => t.DecimalValue, 1.1M, 10.1M, Boundary.Left);
        Assert.Equal("t => ((t.DecimalValue >= 1.1) AndAlso (t.DecimalValue < 10.1))", condition.GetCondition().ToString());

        var criteria2 = new DecimalSegmentCondition<Sample, decimal?>(t => t.NullableDecimalValue, 1.1M, 10.1M, Boundary.Right);
        Assert.Equal("t => ((t.NullableDecimalValue > 1.1) AndAlso (t.NullableDecimalValue <= 10.1))", criteria2.GetCondition().ToString());

        criteria2 = new DecimalSegmentCondition<Sample, decimal?>(t => t.NullableDecimalValue, 1.1M, 10.1M, Boundary.Both);
        Assert.Equal("t => ((t.NullableDecimalValue >= 1.1) AndAlso (t.NullableDecimalValue <= 10.1))", criteria2.GetCondition().ToString());
    }
}
