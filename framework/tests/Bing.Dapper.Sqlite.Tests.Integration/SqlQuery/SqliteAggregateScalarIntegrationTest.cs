using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Bing.Dapper.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>
/// SQLite 真实执行的结构化聚合标量空值测试。
/// </summary>
[Collection(SqliteIntegrationDatabaseCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Database", "Sqlite")]
public sealed class SqliteAggregateScalarIntegrationTest
{
    /// <summary>
    /// 定义聚合标量集成测试使用的表名。
    /// </summary>
    private const string TableName = "sqp_scalar_aggregate_rows";
    /// <summary>
    /// 保存 SQLite 集成测试数据库夹具。
    /// </summary>
    private readonly SqliteIntegrationDatabaseFixture _fixture;

    /// <summary>
    /// 初始化一个 <see cref="SqliteAggregateScalarIntegrationTest" /> 类型的实例。
    /// </summary>
    /// <param name="fixture">SQLite 集成数据库固定装置。</param>
    /// <remarks>
    /// 初始化聚合标量集成测试。
    /// </remarks>
    public SqliteAggregateScalarIntegrationTest(SqliteIntegrationDatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// 验证空输入聚合标量保留空值与计数语义。
    /// </summary>
    [Fact]
    public async Task AggregateScalar_WhenInputIsEmpty_ShouldKeepNullAndCountSemantics()
    {
        await ResetRowsAsync();

        using var root = _fixture.CreateQuery();
        var sum = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id < 0)
            .Select<ScalarRow>((row, functions) => functions.Sum(value => value.Value));
        Assert.Throws<InvalidOperationException>(() => sum.Scalar<decimal>());
        Assert.Null(sum.Scalar<decimal?>());
        Assert.Null(await sum.ScalarAsync<decimal?>());

        var average = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id < 0)
            .Select<ScalarRow>((row, functions) => functions.Avg(value => value.Value));
        Assert.Throws<InvalidOperationException>(() => average.Scalar<decimal>());
        Assert.Null(average.Scalar<decimal?>());

        var minimum = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id < 0)
            .Select<ScalarRow>((row, functions) => functions.Min(value => value.Value));
        Assert.Throws<InvalidOperationException>(() => minimum.Scalar<decimal>());
        Assert.Null(minimum.Scalar<decimal?>());

        var maximum = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id < 0)
            .Select<ScalarRow>((row, functions) => functions.Max(value => value.Value));
        Assert.Throws<InvalidOperationException>(() => maximum.Scalar<decimal>());
        Assert.Null(maximum.Scalar<decimal?>());

        var count = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id < 0)
            .Select<ScalarRow>((row, functions) => functions.CountAll());
        Assert.Equal(0L, count.Scalar<long>());
    }

    /// <summary>
    /// 验证全部为空的输入在同步及异步聚合标量中返回空值。
    /// </summary>
    [Fact]
    public async Task AggregateScalar_WhenAllInputValuesAreNull_ShouldReturnNullSynchronouslyAndAsynchronously()
    {
        await ResetRowsAsync((1, null));

        using var root = _fixture.CreateQuery();
        var sum = root.From<ScalarRow>("r")
            .Where<ScalarRow>(row => row.Id == 1)
            .Select<ScalarRow>((row, functions) => functions.Sum(value => value.Value));

        Assert.Throws<InvalidOperationException>(() => sum.Scalar<decimal>());
        Assert.Null(sum.Scalar<decimal?>());
        Assert.Null(await sum.ScalarAsync<decimal?>());
    }

    /// <summary>
    /// 清空并重新写入聚合测试数据。
    /// </summary>
    /// <param name="rows">测试数据或查询返回的记录集合。</param>
    private async Task ResetRowsAsync(params (int Id, decimal? Value)[] rows)
    {
        await using var connection = new SqliteConnection(_fixture.FirstConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"Drop Table If Exists {TableName}; Create Table {TableName} (Id Integer Not Null Primary Key, Value Decimal Null);";
        await command.ExecuteNonQueryAsync();

        foreach (var row in rows)
        {
            command.Parameters.Clear();
            command.CommandText = $"Insert Into {TableName} (Id, Value) Values (@id, @value);";
            command.Parameters.AddWithValue("@id", row.Id);
            command.Parameters.AddWithValue("@value", row.Value is null ? DBNull.Value : row.Value.Value);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 承载 SQLite 标量聚合测试的行标识和值。
    /// </summary>
    [Table(TableName)]
    private sealed class ScalarRow
    {
        /// <summary>
        /// 获取或设置记录标识。
        /// </summary>
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置参与查询或聚合的测试值。
        /// </summary>
        [Column("Value")]
        public decimal? Value { get; set; }
    }
}
