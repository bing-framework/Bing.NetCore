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