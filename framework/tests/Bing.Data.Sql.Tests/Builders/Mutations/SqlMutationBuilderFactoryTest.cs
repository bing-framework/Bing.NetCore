using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation Builder 工厂测试。
/// </summary>
public sealed class SqlMutationBuilderFactoryTest
{
    /// <summary>
    /// 测试目的：实体命令工厂应创建独立的实体映射 Mutation 命令 Builder。
    /// </summary>
    [Fact]
    public void Create_WhenConfigured_ShouldReturnEntityMutationCommandBuilder()
    {
        // Arrange
        var factory = new SqlEntityMutationCommandBuilderFactory();
        var services = new SqlBuilderServices();

        // Act
        var first = factory.Create(TestMutationSqlProvider.Instance, services);
        var second = factory.Create(TestMutationSqlProvider.Instance, services);

        // Assert
        Assert.IsType<DefaultSqlEntityMutationCommandBuilder>(first);
        Assert.IsType<DefaultSqlEntityMutationCommandBuilder>(second);
        Assert.NotSame(first, second);
    }
}