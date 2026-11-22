using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders;

/// <summary>
/// SQL 查询与写入 Builder 能力隔离测试。
/// </summary>
public sealed class SqlOperationStateTest
{
    /// <summary>
    /// 测试目的：查询 Builder 只暴露查询能力，并维持原查询 SQL。
    /// </summary>
    [Fact]
    public void QueryBuilder_WhenCreated_ShouldExposeQueryCapabilityOnly()
    {
        var query = new TestSqlBuilder().Select("Id").From("samples");

        Assert.False(query is ISqlInsertBuilder);
        Assert.False(query is ISqlUpdateBuilder);
        Assert.False(query is ISqlDeleteBuilder);
        Assert.Equal("Select [Id] \r\nFrom [samples]", query.ToSql());
    }

    /// <summary>
    /// 测试目的：Insert、Update、Delete 各自独立渲染，不共享查询状态或参数。
    /// </summary>
    [Fact]
    public void MutationBuilders_WhenCreatedSeparately_ShouldKeepIndependentSqlAndParameters()
    {
        var insert = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        insert.InsertInto("samples").Columns("Id", "Name").Values(7, "Bing");
        var update = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        update.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "Framework").Where("Id", 8);
        var delete = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        delete.DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 9);

        Assert.Equal("Insert Into [samples] ([Id], [Name]) Values (@_p_0, @_p_1)", insert.ToSql());
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", update.ToSql());
        Assert.Equal("Delete From [samples] Where [Id]=@_p_0", delete.ToSql());
        Assert.Equal(SqlOperationKind.InsertValues, insert.BuildCommand().OperationKind);
        Assert.Equal(SqlOperationKind.Update, update.BuildCommand().OperationKind);
        Assert.Equal(SqlOperationKind.Delete, delete.BuildCommand().OperationKind);
    }

    /// <summary>
    /// 测试目的：专用写入 Builder 的 Clone、New 与 Clear 只影响各自写入状态。
    /// </summary>
    [Fact]
    public void MutationBuilders_WhenClonedClearedAndReused_ShouldKeepIndependentState()
    {
        var insert = new SqlInsertBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        insert.InsertInto("samples").Columns("Id").Values(1);
        var insertClone = insert.Clone();
        var emptyInsert = insert.New();
        insert.Clear();

        var update = new SqlUpdateBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        update.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "Bing").Where("Id", 2);
        var updateClone = update.Clone();
        update.Clear();

        var delete = new SqlDeleteBuilder(TestMutationSqlProvider.Instance, new SqlBuilderServices());
        delete.DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 3);
        var deleteClone = delete.Clone();
        delete.Clear();

        Assert.Equal("Insert Into [samples] ([Id]) Values (@_p_0)", insertClone.ToSql());
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", updateClone.ToSql());
        Assert.Equal("Delete From [samples] Where [Id]=@_p_0", deleteClone.ToSql());

        insert.InsertInto("samples").Columns("Name").Values("later");
        update.Update(new SqlTableReference { TableName = "samples" }).Set("Name", "later").Where("Id", 4);
        delete.DeleteFrom(new SqlTableReference { TableName = "samples" }).Where("Id", 5);
        Assert.Equal("Insert Into [samples] ([Name]) Values (@_p_0)", insert.ToSql());
        Assert.Equal("Update [samples] Set [Name] = @_p_0 Where [Id]=@_p_1", update.ToSql());
        Assert.Equal("Delete From [samples] Where [Id]=@_p_0", delete.ToSql());
        Assert.Equal("Insert Into [samples] ([Id]) Values (@_p_0)", insertClone.ToSql());
        Assert.Equal("Insert Into [samples] ([Id]) Values (@_p_0)", ((SqlInsertBuilder)emptyInsert).InsertInto("samples").Columns("Id").Values(6).ToSql());
    }

    /// <summary>
    /// 测试目的：清空查询 Builder 只重置查询状态，不提供切换为写入 Builder 的入口。
    /// </summary>
    [Fact]
    public void QueryBuilder_WhenCleared_ShouldRemainQueryOnly()
    {
        var query = new TestSqlBuilder().Select("Id").From("samples");

        query.Clear();

        Assert.False(query is ISqlInsertBuilder);
        Assert.False(query is ISqlUpdateBuilder);
        Assert.False(query is ISqlDeleteBuilder);
        Assert.Equal("Select [Name] \r\nFrom [samples]", query.Select("Name").From("samples").ToSql());
    }
}
