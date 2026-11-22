using System.Data;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Bing.Dapper.Tests.SqlQuery;

/// <summary>验证生成式集合绑定后的 SQL 可由 SQLite 实际执行。</summary>
public class SqliteGeneratedCollectionBindingIntegrationTest
{
    [Fact]
    public void BindParameters_WhenCollectionAndScalarNamesOverlap_ShouldExecuteWithCorrectValues()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM (SELECT 1 AS id UNION ALL SELECT 2) " +
            "WHERE id IN @ids AND @ids0=2 AND '@ids'=char(64)||'ids' /* @ids */";

        SqlGeneratedExecution.BindParameters(command, new[]
        {
            new SqlParam("ids", new long[] { 1, 2 }, DbType.Int64),
            new SqlParam("ids0", 2L, DbType.Int64)
        });

        Assert.Equal("SELECT COUNT(*) FROM (SELECT 1 AS id UNION ALL SELECT 2) " +
            "WHERE id IN (@ids0_1,@ids1) AND @ids0=2 AND '@ids'=char(64)||'ids' /* @ids */", command.CommandText);
        Assert.Equal(2L, command.ExecuteScalar());
    }

    [Fact]
    public void BindParameters_WhenCollectionIsEmpty_ShouldReturnNoRows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM (SELECT 1 AS id) WHERE id IN @ids";

        SqlGeneratedExecution.BindParameters(command, new[] { new SqlParam("ids", Array.Empty<long>()) });

        Assert.Equal("SELECT COUNT(*) FROM (SELECT 1 AS id) WHERE id IN (SELECT 1 WHERE 1 = 0)", command.CommandText);
        Assert.Empty(command.Parameters.Cast<SqliteParameter>());
        Assert.Equal(0L, command.ExecuteScalar());
    }
}
