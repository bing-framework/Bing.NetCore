using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// Append SQL 组合测试。
/// </summary>
public class AppendSqlCompositionTest
{
    /// <summary>
    /// 测试目的：结构化 From 与原始 Append Join 可以组合，且 Append 内容保持原样。
    /// </summary>
    [Fact]
    public void StructuredAndAppendSqlCompositionTest()
    {
        var builder = new TestSqlBuilder();

        var sql = builder.Select("o.Id")
            .From("Orders", "o")
            .AppendJoin("(Select 1 As Id) As raw_source On raw_source.Id=o.Id")
            .AppendLeftJoin("[Audit.Log] As audit On audit.OrderId=o.Id")
            .ToSql();

        Assert.Equal("Select [o].[Id] \r\nFrom [Orders] As [o] \r\nJoin (Select 1 As Id) As raw_source On raw_source.Id=o.Id \r\nLeft Join [Audit.Log] As audit On audit.OrderId=o.Id", sql);
    }

    /// <summary>
    /// 测试目的：Append SQL 中的文本别名不得写入结构化别名注册表。
    /// </summary>
    [Fact]
    public void AliasRegistrationBoundaryTest()
    {
        var builder = new TestSqlBuilder();

        builder.AppendFrom("(Select 1) As source").AppendJoin("(Select 2) As joined");
        var exception = Record.Exception(() => builder.Join("Orders", "source"));

        Assert.Null(exception);
        Assert.Equal("Select * \r\nFrom (Select 1) As source \r\nJoin (Select 2) As joined \r\nJoin [Orders] As [source]",
            builder.ToSql());
    }
}