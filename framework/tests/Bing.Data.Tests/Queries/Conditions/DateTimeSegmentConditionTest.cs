﻿using System;
using System.Text;
using Bing.Data.Queries;
using Bing.Data.Queries.Conditions;
using Bing.Data.Tests.Samples;
using Xunit;

namespace Bing.Data.Tests.Queries.Conditions;

/// <summary>
/// 测试日期范围过滤条件 - 包含时间
/// </summary>
public class DateTimeSegmentConditionTest
{
    /// <summary>
    /// 最小日期
    /// </summary>
    private readonly DateTime? _min;
    /// <summary>
    /// 最大日期
    /// </summary>
    private readonly DateTime? _max;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public DateTimeSegmentConditionTest()
    {
        _min = DateTime.Parse("2000-1-1 10:10:10");
        _max = DateTime.Parse("2000-1-2 10:10:10");
    }

    /// <summary>
    /// 测试 - 获取查询条件
    /// </summary>
    [Fact]
    public void Test_GetCondition()
    {
        var result = new StringBuilder();
        result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 10:10:10\"), DateTime))");
        result.Append(" AndAlso (t.DateValue <= Convert(Parse(\"2000/1/2 10:10:10\"), DateTime)))");
        var criteria = new DateTimeSegmentCondition<Sample, DateTime>(t => t.DateValue, _min, _max);
        Assert.Equal(result.ToString(), criteria.GetCondition().ToString());

        result = new StringBuilder();
        result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/1 10:10:10\"), Nullable`1))");
        result.Append(" AndAlso (t.NullableDateValue <= Convert(Parse(\"2000/1/2 10:10:10\"), Nullable`1)))");
        var criteria2 = new DateTimeSegmentCondition<Sample, DateTime?>(t => t.NullableDateValue, _min, _max);
        Assert.Equal(result.ToString(), criteria2.GetCondition().ToString());
    }

    /// <summary>
    /// 测试 - 获取查询条件 - 设置边界
    /// </summary>
    [Fact]
    public void Test_GetCondition_Boundary()
    {
        var result = new StringBuilder();
        result.Append("t => ((t.DateValue > Convert(Parse(\"2000/1/1 10:10:10\"), DateTime))");
        result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/2 10:10:10\"), DateTime)))");
        var criteria = new DateTimeSegmentCondition<Sample, DateTime>(t => t.DateValue, _min, _max, Boundary.Neither);
        Assert.Equal(result.ToString(), criteria.GetCondition().ToString());

        result = new StringBuilder();
        result.Append("t => ((t.DateValue >= Convert(Parse(\"2000/1/1 10:10:10\"), DateTime))");
        result.Append(" AndAlso (t.DateValue < Convert(Parse(\"2000/1/2 10:10:10\"), DateTime)))");
        criteria = new DateTimeSegmentCondition<Sample, DateTime>(t => t.DateValue, _min, _max, Boundary.Left);
        Assert.Equal(result.ToString(), criteria.GetCondition().ToString());

        result = new StringBuilder();
        result.Append("t => ((t.NullableDateValue > Convert(Parse(\"2000/1/1 10:10:10\"), Nullable`1))");
        result.Append(" AndAlso (t.NullableDateValue <= Convert(Parse(\"2000/1/2 10:10:10\"), Nullable`1)))");
        var criteria2 = new DateTimeSegmentCondition<Sample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Right);
        Assert.Equal(result.ToString(), criteria2.GetCondition().ToString());

        result = new StringBuilder();
        result.Append("t => ((t.NullableDateValue >= Convert(Parse(\"2000/1/1 10:10:10\"), Nullable`1))");
        result.Append(" AndAlso (t.NullableDateValue <= Convert(Parse(\"2000/1/2 10:10:10\"), Nullable`1)))");
        criteria2 = new DateTimeSegmentCondition<Sample, DateTime?>(t => t.NullableDateValue, _min, _max, Boundary.Both);
        Assert.Equal(result.ToString(), criteria2.GetCondition().ToString());
    }
}
