using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Microsoft.Data.Sqlite;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 通过 SQLite 实际执行验证 Full Join 来源过滤的关系语义。
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteFullJoinFilterTest
{
    /// <summary>
    /// 双侧租户过滤应保留双方符合租户的未匹配行，并排除跨租户匹配。
    /// </summary>
    [Fact]
    public void FullJoin_WhenBothSidesAreFiltered_ShouldKeepEligibleUnmatchedRows()
    {
        using var connection = CreateDatabase(@"
Insert Into full_filter_left Values (1, 1), (2, 2), (4, 1);
Insert Into full_filter_right Values (10, 99, 1), (11, 1, 1), (12, 2, 1), (13, 99, 2);");
        var builder = new PostgreSqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantFilter()
        }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .FullJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.LeftId)
            .OrderBy("a.Id,b.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select \"a\".\"Id\",\"b\".\"Id\" \r\n" +
            "From (Select * From \"full_filter_left\" As \"a\" Where \"a\".\"TenantId\"=@_p_0) As \"a\" \r\n" +
            "Full Join (Select * From \"full_filter_right\" As \"b\" Where \"b\".\"TenantId\"=@_p_1) As \"b\" " +
            "On \"a\".\"Id\"=\"b\".\"LeftId\" \r\n" +
            "Order By \"a\".\"Id\",\"b\".\"Id\"",
            snapshot.Sql);
        Assert.Equal(new[] { ("@_p_0", 1), ("@_p_1", 1) }, GetIntParameters(snapshot));
        Assert.Equal(new[] { "null:10", "null:12", "1:11", "4:null" }, ExecuteRows(connection, snapshot, 2));
    }

    /// <summary>
    /// 单侧来源的 null 谓词应进入该来源的派生查询并渲染为 IS NULL。
    /// </summary>
    [Fact]
    public void FullJoin_WhenOneSideHasNullFilter_ShouldKeepFilteredAndUnmatchedRows()
    {
        using var connection = CreateDatabase(@"
Insert Into full_filter_left Values (1, 1), (2, 1);
Insert Into full_filter_right Values (10, 1, Null), (11, 1, 1), (12, 99, Null);");
        var builder = new PostgreSqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new NullTenantFilter()
        }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .FullJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.LeftId)
            .OrderBy("a.Id,b.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select \"a\".\"Id\",\"b\".\"Id\" \r\n" +
            "From \"full_filter_left\" As \"a\" \r\n" +
            "Full Join (Select * From \"full_filter_right\" As \"b\" Where \"b\".\"TenantId\" Is Null) As \"b\" " +
            "On \"a\".\"Id\"=\"b\".\"LeftId\" \r\n" +
            "Order By \"a\".\"Id\",\"b\".\"Id\"",
            snapshot.Sql);
        Assert.Empty(snapshot.Parameters);
        Assert.Equal(new[] { "null:12", "1:10", "2:null" }, ExecuteRows(connection, snapshot, 2));
    }

    /// <summary>
    /// Full Join 前的 Left Join 右侧来源即使位于早期非保留输入，也必须先过滤来源。
    /// </summary>
    [Fact]
    public void FullJoin_AfterLeftJoin_ShouldPrefilterEarlierNonPreservedSource()
    {
        using var connection = CreateDatabase(@"
Insert Into full_filter_left Values (1, 1), (2, 1);
Insert Into full_filter_middle Values (10, 1, 1), (20, 2, 2);
Insert Into full_filter_final Values (100, 10, 1), (200, 20, 1), (300, 99, 1);");
        var builder = new PostgreSqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantFilter(typeof(MiddleRow))
        }));
        builder.Select("a.Id,b.Id,c.Id").From<LeftRow>("a")
            .LeftJoin<MiddleRow>("b").On<LeftRow, MiddleRow>((left, middle) => left.Id == middle.LeftId)
            .FullJoin<FinalRow>("c").On<MiddleRow, FinalRow>((middle, final) => middle.Id == final.MiddleId)
            .OrderBy("a.Id,b.Id,c.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select \"a\".\"Id\",\"b\".\"Id\",\"c\".\"Id\" \r\n" +
            "From \"full_filter_left\" As \"a\" \r\n" +
            "Left Join (Select * From \"full_filter_middle\" As \"b\" Where \"b\".\"TenantId\"=@_p_0) As \"b\" " +
            "On \"a\".\"Id\"=\"b\".\"LeftId\" \r\n" +
            "Full Join \"full_filter_final\" As \"c\" On \"b\".\"Id\"=\"c\".\"MiddleId\" \r\n" +
            "Order By \"a\".\"Id\",\"b\".\"Id\",\"c\".\"Id\"",
            snapshot.Sql);
        Assert.Equal(new[] { ("@_p_0", 1) }, GetIntParameters(snapshot));
        Assert.Equal(
            new[] { "null:null:200", "null:null:300", "1:10:100", "2:null:null" },
            ExecuteRows(connection, snapshot, 3));
    }

    /// <summary>
    /// 创建供单个测试使用的共享内存数据库。
    /// </summary>
    /// <param name="seedSql">该测试的数据集。</param>
    /// <returns>保持共享内存数据库存活的 SQLite 连接。</returns>
    private static SqliteConnection CreateDatabase(string seedSql)
    {
        var connection = new SqliteConnection(
            $"Data Source=file:full-filter-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
Create Table full_filter_left (Id Integer Not Null, TenantId Integer Not Null);
Create Table full_filter_right (Id Integer Not Null, LeftId Integer Not Null, TenantId Integer);
Create Table full_filter_middle (Id Integer Not Null, LeftId Integer Not Null, TenantId Integer Not Null);
Create Table full_filter_final (Id Integer Not Null, MiddleId Integer Not Null, TenantId Integer Not Null);
" + seedSql;
        command.ExecuteNonQuery();
        return connection;
    }

    /// <summary>
    /// 将 Builder 参数快照转成可直接比较的名称和值对。
    /// </summary>
    private static (string Name, int Value)[] GetIntParameters(SqlBuilderExecutionSnapshot snapshot) =>
        snapshot.Parameters.Select(parameter => (parameter.Name, Convert.ToInt32(parameter.Value))).ToArray();

    /// <summary>
    /// 使用 SQLite 执行 Builder 的 SQL/参数快照，并返回指定列数的排序结果。
    /// </summary>
    private static string[] ExecuteRows(SqliteConnection connection, SqlBuilderExecutionSnapshot snapshot, int columnCount)
    {
        using var command = connection.CreateCommand();
        command.CommandText = snapshot.Sql;
        foreach (var parameter in snapshot.Parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = new string[columnCount];
            for (var index = 0; index < columnCount; index++)
                values[index] = reader.IsDBNull(index) ? "null" : Convert.ToString(reader.GetValue(index));
            rows.Add(string.Join(":", values));
        }
        return rows.ToArray();
    }

    /// <summary>
    /// 为全部来源或指定实体来源追加租户过滤参数。
    /// </summary>
    private sealed class TenantFilter : ISqlFilter
    {
        private readonly Type _entityType;

        public TenantFilter(Type entityType = null) => _entityType = entityType;

        public void Filter(SqlFilterContext context)
        {
            foreach (var source in context.Sources)
            {
                if (_entityType != null && source.EntityType != _entityType)
                    continue;
                context.AddPredicate(source, context.GetColumn(source, nameof(LeftRow.TenantId), required: true), 1);
            }
        }
    }

    /// <summary>
    /// 仅为右侧来源提交 null 租户谓词。
    /// </summary>
    private sealed class NullTenantFilter : ISqlFilter
    {
        public void Filter(SqlFilterContext context)
        {
            var source = context.Sources.Single(item => item.EntityType == typeof(RightRow));
            context.AddPredicate(source, context.GetColumn(source, nameof(RightRow.TenantId), required: true), null);
        }
    }

    [Table("full_filter_left")]
    private sealed class LeftRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("full_filter_right")]
    private sealed class RightRow
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public int? TenantId { get; set; }
    }

    [Table("full_filter_middle")]
    private sealed class MiddleRow
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public int TenantId { get; set; }
    }

    [Table("full_filter_final")]
    private sealed class FinalRow
    {
        public int Id { get; set; }
        public int MiddleId { get; set; }
        public int TenantId { get; set; }
    }
}
