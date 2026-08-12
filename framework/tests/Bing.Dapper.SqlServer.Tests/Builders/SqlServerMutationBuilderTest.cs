using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Metadata;

namespace Bing.Dapper.Tests.Builders;

/// <summary>
/// SQL Server 实体写入 SQL 生成器测试。
/// </summary>
public sealed class SqlServerMutationBuilderTest
{
    /// <summary>
    /// 测试目的：SQL Server 插入应使用方括号标识符和标准参数前缀。
    /// </summary>
    [Fact]
    public void Insert_WhenMappedEntityIsProvided_ShouldRenderSqlServerSql()
    {
        // Arrange
        var builder = new DefaultSqlEntityMutationCommandBuilder(SqlServerSqlProvider.Instance, new SqlBuilderServices());

        // Act
        var command = builder.Insert(new MutationSample { Name = "Bing" });

        // Assert
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", command.Sql);
        Assert.Equal("@_p_0", Assert.Single(command.Parameters).Name);
    }

    /// <summary>
    /// 测试目的：未声明 Update From 能力的 Provider 应在渲染前明确拒绝，不能输出不可执行方言 SQL。
    /// </summary>
    [Fact]
    public void UpdateFrom_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new SqlServerBuilder()
            .Update(new SqlTableReference { TableName = "samples", Alias = "t" })
            .UpdateFrom(new SqlTableReference { TableName = "sample_updates", Alias = "s" })
            .SetFrom("Name", "Name")
            .WhereFrom("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.sqlserver 不支持 Update From。", exception.Message);
        Assert.False(SqlServerSqlProvider.Instance.Profile.Mutation.SupportsUpdateFrom);
    }

    /// <summary>
    /// 测试目的：未声明 Delete Using 能力的 Provider 应在渲染前明确拒绝。
    /// </summary>
    [Fact]
    public void DeleteUsing_WhenProviderDoesNotSupportIt_ShouldThrowNotSupportedException()
    {
        // Arrange
        var builder = new SqlServerBuilder()
            .DeleteFrom(new SqlTableReference { TableName = "samples", Alias = "t" })
            .DeleteUsing(new SqlTableReference { TableName = "sample_deletes", Alias = "s" })
            .WhereUsing("Id", "Id");

        // Act
        var exception = Assert.Throws<NotSupportedException>(() => builder.ToSql());

        // Assert
        Assert.Equal("Provider bing.sqlserver 不支持 Delete Using。", exception.Message);
        Assert.False(SqlServerSqlProvider.Instance.Profile.Mutation.SupportsDeleteUsing);
    }

    /// <summary>
    /// 测试目的：SQL Server Insert Values 应在 Values 之前输出 INSERTED 投影。
    /// </summary>
    [Fact]
    public void Returning_WhenInsertValuesIsConfigured_ShouldRenderOutputBeforeValues()
    {
        // Arrange
        var builder = new SqlServerBuilder()
            .InsertInto(new SqlTableReference { Schema = "dbo", TableName = "samples" })
            .Columns("Name")
            .Values("Bing")
            .Returning("Id", "Name");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [dbo].[samples] ([Name]) Output [Inserted].[Id], [Inserted].[Name] " +
                     "Values (@_p_0)", sql);
    }

    /// <summary>
    /// 测试目的：SQL Server Insert Select 应在查询来源之前输出 INSERTED 投影。
    /// </summary>
    [Fact]
    public void Returning_WhenInsertSelectIsConfigured_ShouldRenderOutputBeforeSelect()
    {
        // Arrange
        ISqlBuilder builder = new SqlServerBuilder();
        builder.InsertInto(new SqlTableReference { Schema = "dbo", TableName = "archive_samples" })
            .Columns("Id", "Name")
            .Select("Id", "Name")
            .From("samples")
            .Returning("Id");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [dbo].[archive_samples] ([Id], [Name]) Output [Inserted].[Id] \r\n" +
                     "Select [Id],[Name] \r\nFrom [samples]", sql);
    }

    /// <summary>
    /// 测试目的：SQL Server Update 应在筛选条件之前输出 INSERTED 投影。
    /// </summary>
    [Fact]
    public void Returning_WhenUpdateIsConfigured_ShouldRenderInsertedOutputBeforeWhere()
    {
        // Arrange
        ISqlBuilder builder = new SqlServerBuilder();
        builder.Update(new SqlTableReference { Schema = "dbo", TableName = "samples" })
            .Set("Name", "Bing")
            .Where("Id", 1)
            .Returning("Id", "Name");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Update [dbo].[samples] Set [Name] = @_p_0 Output [Inserted].[Id], [Inserted].[Name] " +
                     "Where [Id]=@_p_1", sql);
    }

    /// <summary>
    /// 测试目的：SQL Server Delete 应通过 DELETED 返回删除前的目标行。
    /// </summary>
    [Fact]
    public void Returning_WhenDeleteIsConfigured_ShouldRenderDeletedOutputBeforeWhere()
    {
        // Arrange
        ISqlBuilder builder = new SqlServerBuilder();
        builder.DeleteFrom(new SqlTableReference { Schema = "dbo", TableName = "samples" })
            .Where("Id", 1)
            .Returning("Id", "Name");

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Delete From [dbo].[samples] Output [Deleted].[Id], [Deleted].[Name] Where [Id]=@_p_0", sql);
    }

    /// <summary>
    /// 测试目的：SQL Server 实体返回投影应输出物理列及 CLR 属性别名。
    /// </summary>
    [Fact]
    public void Returning_WhenMappedProjectionIsConfigured_ShouldRenderOutputWithClrAliases()
    {
        // Arrange
        ISqlBuilder builder = new SqlServerBuilder();
        builder.Update(new SqlTableReference { Schema = "dbo", TableName = "returning_samples" })
            .Set("sample_name", "Bing")
            .Where("sample_id", 7)
            .Returning<ReturningMutationSample>(item => new { item.Id, item.Name });

        // Act
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Update [dbo].[returning_samples] Set [sample_name] = @_p_0 " +
                     "Output [Inserted].[sample_id] As [Id], [Inserted].[sample_name] As [Name] " +
                     "Where [sample_id]=@_p_1", sql);
        Assert.True(SqlServerSqlProvider.Instance.Profile.Mutation.SupportsReturning);
    }

    /// <summary>
    /// 测试 - SQL Server Delete 的 OUTPUT 投影应随 Clone 保留，并在源 Builder Clear 后保持独立。
    /// </summary>
    [Fact]
    public void Output_WhenDeleteBuilderIsClonedAndSourceCleared_ShouldRetainDeletedProjectionOnlyInClone()
    {
        // Arrange
        var source = new SqlServerBuilder();
        source.DeleteFrom(new SqlTableReference { Schema = "dbo", TableName = "samples" })
            .Returning("Id", "Name");
        ((IMutationWhereClauseAccessor)source).WhereClause.And(new EqualCondition("[Id]", "@_p_0"));
        ((ISqlCommonPartAccessor)source).ParameterManager.Add("@_p_0", 1);

        // Act
        var clone = source.Clone();
        source.Clear();
        source.DeleteFrom(new SqlTableReference { Schema = "dbo", TableName = "samples" });
        ((IMutationWhereClauseAccessor)source).WhereClause.And(new EqualCondition("[Id]", "@_p_0"));
        ((ISqlCommonPartAccessor)source).ParameterManager.Add("@_p_0", 2);

        // Assert
        Assert.Equal("Delete From [dbo].[samples] Output [Deleted].[Id], [Deleted].[Name] Where [Id]=@_p_0",
            clone.ToSql());
        Assert.Equal("Delete From [dbo].[samples] Where [Id]=@_p_0", source.ToSql());
    }

    /// <summary>
    /// 测试 - SQL Server Insert Select 在 Clear 后重用时不得残留此前的 OUTPUT 投影。
    /// </summary>
    [Fact]
    public void Output_WhenInsertSelectBuilderIsClearedAndReused_ShouldNotLeakBeforeSourceProjection()
    {
        // Arrange
        var builder = new SqlServerBuilder()
            .InsertInto(new SqlTableReference { Schema = "dbo", TableName = "archive_samples" })
            .Columns("Id")
            .Select("Id")
            .From("samples")
            .Returning("Id");

        // Act
        builder.ToSql();
        builder.Clear()
            .InsertInto(new SqlTableReference { Schema = "dbo", TableName = "archive_samples" })
            .Columns("Id")
            .Select("Id")
            .From("samples");
        var sql = builder.ToSql();

        // Assert
        Assert.Equal("Insert Into [dbo].[archive_samples] ([Id]) \r\nSelect [Id] \r\nFrom [samples]", sql);
    }

    /// <summary>
    /// SQL Server 样例实体。
    /// </summary>
    [Table("samples")]
    private sealed class MutationSample
    {
        /// <summary>
        /// 名称。
        /// </summary>
        public string Name { get; set; }
    }

    /// <summary>
    /// SQL Server Returning 映射样例实体。
    /// </summary>
    [Table("returning_samples", Schema = "dbo")]
    private sealed class ReturningMutationSample
    {
        /// <summary>
        /// 标识。
        /// </summary>
        [Column("sample_id")]
        public int Id { get; set; }

        /// <summary>
        /// 名称。
        /// </summary>
        [Column("sample_name")]
        public string Name { get; set; }
    }
}