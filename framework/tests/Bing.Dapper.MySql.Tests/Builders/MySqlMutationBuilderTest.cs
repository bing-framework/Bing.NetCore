using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// MySQL 实体写入 SQL 生成器测试。
/// </summary>
public sealed class MySqlMutationBuilderTest
{
    /// <summary>
    /// 测试目的：MySQL 插入应使用反引号标识符和标准参数前缀。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderMySqlSql()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(MySqlSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into `samples` (`Name`) Values (@_p_0)", command.Sql);
        Assert.Equal("@_p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// MySQL 样例实体。
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