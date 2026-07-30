using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Mutation Builder 工厂测试。
/// </summary>
public sealed class SqlMutationBuilderFactoryTest
{
    /// <summary>
    /// 测试目的：工厂应创建相互独立的 Insert、Update 与 Delete 专用 Builder，并保留各自的 Fluent 输出能力。
    /// </summary>
    [Fact]
    public void CreateSpecializedBuilders_WhenConfigured_ShouldRenderIndependentSql()
    {
        // Arrange
        var factory = new SqlFluentMutationBuilderFactory();
        var services = new SqlBuilderServices();
        var table = new SqlTableReference { TableName = "samples" };

        // Act
        var insert = factory.CreateInsert(TestMutationSqlProvider.Instance, services)
            .InsertInto(table)
            .Columns("Name")
            .Values("Bing");
        var update = factory.CreateUpdate(TestMutationSqlProvider.Instance, services)
            .Update(table)
            .Set("Name", "Framework")
            .Where(new EqualCondition("[Id]", "@_p_1"));
        var delete = factory.CreateDelete(TestMutationSqlProvider.Instance, services)
            .DeleteFrom(table)
            .AllowAllRows();

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", insert.ToSql());
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", update.ToSql());
        Assert.Equal("Delete From [samples]", delete.ToSql());
    }

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