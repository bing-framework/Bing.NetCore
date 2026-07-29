using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// Oracle 实体写入 SQL 生成器测试。
/// </summary>
public sealed class OracleMutationBuilderTest
{
    /// <summary>
    /// 测试目的：Oracle Provider 必须显式声明不支持标准多行 Values，供 Auto 批处理安全回退。
    /// </summary>
    [Fact]
    public void Capabilities_WhenResolved_ShouldDisableStandardMultiRowValues()
    {
        // Arrange / Act
        var capabilities = OracleSqlProvider.Instance.Capabilities;

        // Assert
        Assert.False(capabilities.SupportsMultiRowValues);
    }

    /// <summary>
    /// 测试目的：Oracle 插入应使用双引号标识符和无前缀的执行参数名称。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderOracleSql()
    {
        // Arrange
        var builder = new DefaultSqlMutationBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into \"samples\" (\"Name\") Values (p_0)", command.Sql);
        Assert.Equal(":p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：Oracle 不支持标准多行 Values 语法时，组合 Insert 应在 SQL 生成前拒绝。
    /// </summary>
    [Fact]
    public void InsertCombined_WhenMultipleEntitiesAreProvided_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new DefaultSqlMutationBuilder(OracleSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.InsertCombined(new[]
        {
            new MutationSample { Name = "first" },
            new MutationSample { Name = "second" }
        }));

        // Assert
        Assert.Equal("Provider bing.oracle 不支持多行 Values。", exception.Message);
    }

    /// <summary>
    /// Oracle 样例实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }
}