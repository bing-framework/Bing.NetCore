using System.ComponentModel.DataAnnotations.Schema;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Microsoft.Data.Sqlite;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// 通过 SQLite 实际执行验证 Right Join 全局过滤的关系语义。
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteRightJoinFilterTest
{
    /// <summary>
    /// 右侧来源过滤应保留符合租户的未匹配行，并排除其他租户的右侧行。
    /// </summary>
    [Fact]
    public void RightJoin_WhenRightSideIsFiltered_ShouldKeepUnmatchedRowsAndExcludeOtherTenants()
    {
        using var connection = CreateDatabase();
        var builder = new MySqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantFilter(typeof(RightRow))
        }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.LeftId)
            .OrderBy("b.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select `a`.`Id`,`b`.`Id` \r\n" +
            "From `right_filter_left` As `a` \r\n" +
            "Right Join `right_filter_rows` As `b` On `a`.`Id`=`b`.`LeftId` \r\n" +
            "Where `b`.`TenantId`=@_p_0 \r\n" +
            "Order By `b`.`Id`",
            snapshot.Sql);
        Assert.Equal(new[] { "1:10", "null:20", "2:21" }, ExecuteRows(connection, snapshot));
    }

    /// <summary>
    /// 两侧来源均过滤时，左侧过滤放入首个 On，符合租户的未匹配右侧行仍应保留。
    /// </summary>
    [Fact]
    public void RightJoin_WhenBothSidesAreFiltered_ShouldKeepEligibleUnmatchedRightRows()
    {
        using var connection = CreateDatabase();
        var builder = new MySqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantFilter()
        }));
        builder.Select("a.Id,b.Id").From<LeftRow>("a")
            .RightJoin<RightRow>("b").On<LeftRow, RightRow>((left, right) => left.Id == right.LeftId)
            .OrderBy("b.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select `a`.`Id`,`b`.`Id` \r\n" +
            "From `right_filter_left` As `a` \r\n" +
            "Right Join `right_filter_rows` As `b` On `a`.`Id`=`b`.`LeftId` And `a`.`TenantId`=@_p_0 \r\n" +
            "Where `b`.`TenantId`=@_p_1 \r\n" +
            "Order By `b`.`Id`",
            snapshot.Sql);
        Assert.Equal(new[] { "null:10", "null:20", "2:21" }, ExecuteRows(connection, snapshot));
    }

    /// <summary>
    /// 后续 Right Join 使中间来源可空时，其中间来源的租户谓词应进入后续 On，保留最终侧未匹配行。
    /// </summary>
    [Fact]
    public void RightJoin_WhenLaterRightJoinMakesFilteredSourceNullable_ShouldKeepFinalRows()
    {
        using var connection = CreateDatabase();
        var builder = new MySqlBuilder(new SqlBuilderServices(filters: new ISqlFilter[]
        {
            new TenantFilter(typeof(MiddleRow))
        }));
        builder.Select("b.Id,c.Id").From<LeftRow>("a")
            .RightJoin<MiddleRow>("b").On<LeftRow, MiddleRow>((left, middle) => left.Id == middle.LeftId)
            .RightJoin<FinalRow>("c").On<MiddleRow, FinalRow>((middle, final) => middle.Id == final.MiddleId)
            .OrderBy("c.Id");

        var snapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);

        Assert.Equal(
            "Select `b`.`Id`,`c`.`Id` \r\n" +
            "From `right_filter_left` As `a` \r\n" +
            "Right Join `right_filter_middle` As `b` On `a`.`Id`=`b`.`LeftId` \r\n" +
            "Right Join `right_filter_final` As `c` On `b`.`Id`=`c`.`MiddleId` And `b`.`TenantId`=@_p_0 \r\n" +
            "Order By `c`.`Id`",
            snapshot.Sql);
        Assert.Equal(new[] { "null:101", "12:102", "null:103" }, ExecuteRows(connection, snapshot));
    }

    /// <summary>
    /// 创建带有跨租户、匹配及未匹配行的共享内存数据库。
    /// </summary>
    /// <returns>保持共享内存数据库存活的 SQLite 连接。</returns>
    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection(
            $"Data Source=file:right-filter-{Guid.NewGuid():N}?mode=memory&cache=shared");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
Create Table right_filter_left (Id Integer Not Null, TenantId Integer Not Null);
Create Table right_filter_rows (Id Integer Not Null, LeftId Integer Not Null, TenantId Integer Not Null);
Create Table right_filter_middle (Id Integer Not Null, LeftId Integer Not Null, TenantId Integer Not Null);
Create Table right_filter_final (Id Integer Not Null, MiddleId Integer Not Null, TenantId Integer Not Null);
Insert Into right_filter_left Values (1, 9), (2, 1);
Insert Into right_filter_rows Values (10, 1, 1), (20, 99, 1), (21, 2, 1), (30, 2, 9);
Insert Into right_filter_middle Values (11, 1, 9), (12, 99, 1);
Insert Into right_filter_final Values (101, 11, 1), (102, 12, 1), (103, 99, 1);";
        command.ExecuteNonQuery();
        return connection;
    }

    /// <summary>
    /// 使用 SQLite 执行 Builder 的 SQL/参数快照，并返回两列结果。
    /// </summary>
    /// <param name="connection">共享内存数据库连接。</param>
    /// <param name="snapshot">Builder 生成的 SQL 和参数。</param>
    /// <returns>按查询顺序排列的两列文本值。</returns>
    private static string[] ExecuteRows(SqliteConnection connection, SqlBuilderExecutionSnapshot snapshot)
    {
        using var command = connection.CreateCommand();
        command.CommandText = snapshot.Sql;
        foreach (var parameter in snapshot.Parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);

        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var left = reader.IsDBNull(0) ? "null" : Convert.ToString(reader.GetValue(0));
            var right = reader.IsDBNull(1) ? "null" : Convert.ToString(reader.GetValue(1));
            rows.Add($"{left}:{right}");
        }
        return rows.ToArray();
    }

    /// <summary>
    /// 为指定实体来源追加租户参数的 SQL 全局过滤器。
    /// </summary>
    private sealed class TenantFilter : ISqlFilter
    {
        private readonly Type _entityType;

        /// <summary>
        /// 初始化一个 <see cref="TenantFilter" /> 类型的实例。
        /// </summary>
        /// <param name="entityType">仅过滤指定实体类型；为空时过滤所有来源。</param>
        public TenantFilter(Type entityType = null) => _entityType = entityType;

        /// <inheritdoc />
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

    [Table("right_filter_left")]
    private sealed class LeftRow
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
    }

    [Table("right_filter_rows")]
    private sealed class RightRow
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public int TenantId { get; set; }
    }

    [Table("right_filter_middle")]
    private sealed class MiddleRow
    {
        public int Id { get; set; }
        public int LeftId { get; set; }
        public int TenantId { get; set; }
    }

    [Table("right_filter_final")]
    private sealed class FinalRow
    {
        public int Id { get; set; }
        public int MiddleId { get; set; }
        public int TenantId { get; set; }
    }
}
