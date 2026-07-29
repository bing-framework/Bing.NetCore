using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Insert Mutation Builder 测试。
/// </summary>
public sealed class SqlInsertBuilderTest
{
    /// <summary>
    /// 测试目的：Insert Fluent API 应返回原 Builder，并按子句顺序输出参数化多行 Values SQL。
    /// </summary>
    [Fact]
    public void InsertInto_WhenColumnsAndValuesConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var result = builder.InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name), nameof(MutationSample.Age))
            .Values("Bing", 18)
            .Values(new[] { (object)"Framework", 20 });

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Insert Into [samples] ([Name], [Age]) Values (@_p_0, @_p_1), (@_p_2, @_p_3)", builder.ToSql());
        Assert.Equal(4, builder.GetParameters().Count);
    }

    /// <summary>
    /// 测试目的：Values 行列数量不一致时应在状态写入前抛出明确异常。
    /// </summary>
    [Fact]
    public void Values_WhenRowColumnCountDiffers_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name), nameof(MutationSample.Age))
            .Values("Bing", 18);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Values("Framework"));

        // Assert
        Assert.Equal("Insert Values 行列数量不一致。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clone 应复制 Insert Clause 状态和参数，同时保持后续写入隔离。
    /// </summary>
    [Fact]
    public void Clone_WhenSourceContainsValues_ShouldKeepInstancesIndependent()
    {
        // Arrange
        var source = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .InsertInto<SqlInsertBuilder, MutationSample>()
            .Columns(nameof(MutationSample.Name))
            .Values("source");

        // Act
        var clone = (SqlInsertBuilder)source.Clone();
        clone.Values("clone");

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", source.ToSql());
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0), (@_p_1)", clone.ToSql());
    }

    /// <summary>
    /// 映射到测试表的 Insert 实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 年龄。
        /// </summary>
        public int Age { get; set; }
    }
}