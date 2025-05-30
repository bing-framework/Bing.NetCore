﻿using Bing.Data.Sql.Tests.Samples;
using Xunit.Abstractions;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Sql生成器测试
/// </summary>
public partial class SqlBuilderTest
{
    /// <summary>
    /// 输出工具
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 测试Sql生成器
    /// </summary>
    private TestSqlBuilder _builder;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public SqlBuilderTest(ITestOutputHelper output)
    {
        _output = output;
        _builder = new TestSqlBuilder();
    }
}
