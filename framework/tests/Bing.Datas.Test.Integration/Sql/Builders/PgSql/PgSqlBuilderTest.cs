﻿using Bing.Datas.Dapper.PgSql;
using Bing.Data.Sql;
using Xunit;
using Xunit.Abstractions;
using Str = Bing.Helpers.Str;

namespace Bing.Data.Test.Integration.Sql.Builders.PgSql;

/// <summary>
/// PgSql Sql生成器测试
/// </summary>
public class PgSqlBuilderTest : TestBase
{
    /// <summary>
    /// PgSql Sql生成器
    /// </summary>
    private readonly PgSqlBuilder _builder;

    /// <summary>
    /// 初始化一个<see cref="PgSqlBuilderTest"/>类型的实例
    /// </summary>
    public PgSqlBuilderTest(ITestOutputHelper output) : base(output)
    {
        _builder = new PgSqlBuilder();
    }

    /// <summary>
    /// 测试输出的调试SQL - 布尔值输出false，而不是0
    /// </summary>
    [Fact]
    public void Test_1()
    {
        //结果
        var result = new Str();
        result.AppendLine("Select * ");
        result.AppendLine("From \"Test\" ");
        result.Append("Where \"A\"=1 And \"B\"=2 And \"C\"=false And \"D\"=true And \"E\"=5 And \"F\"=6 And ");
        result.Append("\"G\"=7 And \"H\"=8 And \"I\"=9 And \"J\"=10 And \"K\"=11 And \"L\"=12");

        //执行
        _builder.Select("*")
            .From("Test")
            .Where("A", 1)
            .Where("B", 2)
            .Where("C", false)
            .Where("D", true)
            .Where("E", 5)
            .Where("F", 6)
            .Where("G", 7)
            .Where("H", 8)
            .Where("I", 9)
            .Where("J", 10)
            .Where("K", 11)
            .Where("L", 12);

        //验证
        Assert.Equal(result.ToString(), _builder.ToDebugSql());
    }
}