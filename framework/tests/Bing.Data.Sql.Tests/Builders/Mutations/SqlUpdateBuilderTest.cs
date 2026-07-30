using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Mutations;

/// <summary>
/// Update Mutation Builder 测试。
/// </summary>
public sealed class SqlUpdateBuilderTest
{
    /// <summary>
    /// 测试目的：Update Fluent API 应按 Update、Set、Where 顺序输出 SQL，并返回原 Builder。
    /// </summary>
    [Fact]
    public void Update_WhenSetAndWhereConfigured_ShouldRenderExpectedSql()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        builder.MutationContext.ParameterManager.Add("@_p_1", 7);

        // Act
        var result = builder.Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .Where(new EqualCondition("[Id]", "@_p_1"));

        // Assert
        Assert.Same(builder, result);
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", builder.ToSql());
        Assert.Equal(2, builder.GetParameters().Count);
    }

    /// <summary>
    /// 测试目的：未显式允许时，无 Where 的 Update 必须被拒绝。
    /// </summary>
    [Fact]
    public void ToSql_WhenWhereIsMissingAndAllRowsNotAllowed_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing");

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => builder.ToSql());

        // Assert
        Assert.Equal("拒绝执行无条件 Update 操作。", exception.Message);
    }

    /// <summary>
    /// 测试目的：Clear 后 Builder 应保留共享配置并清空所有 Mutation 状态。
    /// </summary>
    [Fact]
    public void Clear_WhenBuilderHasState_ShouldAllowReuseWithoutPreviousState()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update(new SqlTableReference { TableName = "samples" })
            .Set("Name", "Bing")
            .AllowAllRows();

        // Act
        builder.Clear();
        builder.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "Framework").AllowAllRows();

        // Assert
        Assert.Equal("Update [samples] Set [Name] = @_p_0", builder.ToSql());
        Assert.Single(builder.GetParameters());
    }

    /// <summary>
    /// 测试目的：强类型 Set 与 Where 应使用实体映射列和带元数据参数输出完整 Update SQL。
    /// </summary>
    [Fact]
    public void Update_WhenTypedSetAndWhereConfigured_ShouldRenderMappedParameterizedSql()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());

        // Act
        builder.Update<TypedMutationSample>()
            .Set<TypedMutationSample, string>(item => item.Name, "Bing")
            .Where<TypedMutationSample, int>(item => item.Id, 7);

        // Assert
        var command = builder.BuildCommand();
        Assert.Equal("Update [typed_samples] Set [Name] = @_p_0 Where [Id]=@_p_1", command.Sql);
        Assert.Equal(new object[] { "Bing", 7 }, command.Parameters.Select(item => item.Value));
        Assert.All(command.Parameters, item => Assert.Equal(SqlParameterMetadataLevel.Full, item.MetadataLevel));
    }

    /// <summary>
    /// 测试目的：强类型 Set 不得更新实体主键，避免 Fluent API 绕过实体写入安全规则。
    /// </summary>
    [Fact]
    public void Set_WhenTypedPrimaryKeyIsProvided_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var builder = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices())
            .Update<TypedMutationSample>();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.Set<TypedMutationSample, int>(item => item.Id, 7));

        // Assert
        Assert.Equal("实体 TypedMutationSample 的属性 Id 不能用于更新。", exception.Message);
    }

    /// <summary>
    /// 强类型 Fluent Mutation 的映射实体。
    /// </summary>
    [Table("typed_samples")]
    private sealed class TypedMutationSample
    {
        /// <summary>
        /// 主键。
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 可更新名称。
        /// </summary>
        public string Name { get; set; }
    }
}