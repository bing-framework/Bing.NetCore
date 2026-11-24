using System;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>对比 SQLite 实体整行 Returning 命令的逐次渲染与生成 SQL 常量路径。</summary>
[MemoryDiagnoser]
public class SqlGeneratedReturningBenchmarks
{
    private const string InsertSql =
        "Insert Into \u0060generated_returning_rows\u0060 (\u0060Id\u0060, \u0060Name\u0060, \u0060Version\u0060) " +
        "Values (@_p_0, @_p_1, @_p_2) Returning \u0060Id\u0060 As \u0060Id\u0060, " +
        "\u0060Name\u0060 As \u0060Name\u0060, \u0060Version\u0060 As \u0060Version\u0060";
    private const string UpdateSql =
        "Update \u0060generated_returning_rows\u0060 Set \u0060Name\u0060 = @_p_0, \u0060Version\u0060 = @_p_1 " +
        "Where \u0060Id\u0060 = @_p_2 And \u0060Version\u0060 = @_p_3 " +
        "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
        "\u0060Version\u0060 As \u0060Version\u0060";
    private const string DeleteSql =
        "Delete From \u0060generated_returning_rows\u0060 Where \u0060Id\u0060 = @_p_0 And \u0060Version\u0060 = @_p_1 " +
        "Returning \u0060Id\u0060 As \u0060Id\u0060, \u0060Name\u0060 As \u0060Name\u0060, " +
        "\u0060Version\u0060 As \u0060Version\u0060";

    private readonly ReturningRow _row = new() { Id = 7, Name = "sample", Version = 2 };
    private readonly SqlUpdateOptions<ReturningRow> _updateOptions =
        new SqlUpdateOptions<ReturningRow>().Original(row => row.Version, 1);
    private readonly SqlDeleteOptions<ReturningRow> _deleteOptions =
        new SqlDeleteOptions<ReturningRow>().Original(row => row.Version, 1);
    private DefaultSqlEntityMutationCommandBuilder _builder;

    /// <summary>单实体写入操作。</summary>
    [Params("Insert", "Update", "Delete")]
    public string Operation { get; set; } = "Insert";

    /// <summary>是否登记整行 Returning SQL 常量。</summary>
    [Params(false, true)]
    public bool UseGeneratedSql { get; set; }

    /// <summary>使用一致的实体 Getter、映射和参数绑定设置，并核对完整 SQL。</summary>
    [GlobalSetup]
    public void Setup()
    {
        var model = new EntityModelMetadata(typeof(ReturningRow), "generated_returning_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Id), typeof(int), true, true,
                isKey: true, generatedGetter: source => ((ReturningRow)source).Id),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Name), typeof(string), true, true,
                generatedGetter: source => ((ReturningRow)source).Name),
            EntityPropertyMetadata.CreateGenerated(nameof(ReturningRow.Version), typeof(int), true, true,
                isConcurrencyToken: true, generatedGetter: source => ((ReturningRow)source).Version)
        }, () => new ReturningRow())
        {
            GeneratedMutationReturningSql = UseGeneratedSql ? GetSql : null
        };
        _builder = new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance, new SqlBuilderServices(
            metadataOptions: new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly },
            entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));

        var command = BuildCommand();
        var expectedSql = Operation switch
        {
            "Insert" => InsertSql,
            "Update" => UpdateSql,
            "Delete" => DeleteSql,
            _ => throw new InvalidOperationException("Unknown mutation operation.")
        };
        if (command.Sql != expectedSql || !command.HasReturning || !command.IsGeneratedExecution ||
            UseGeneratedSql && !ReferenceEquals(command.Sql, expectedSql))
            throw new InvalidOperationException("Generated Returning benchmark did not reach the expected SQL path.");
    }

    /// <summary>构造命令和参数快照，不执行数据库 I/O。</summary>
    [Benchmark]
    public SqlWriteCommand BuildCommand() => Operation switch
    {
        "Insert" => _builder.InsertReturning(_row),
        "Update" => _builder.UpdateReturning(_row, _updateOptions),
        "Delete" => _builder.DeleteReturning(_row, _deleteOptions),
        _ => throw new InvalidOperationException("Unknown mutation operation.")
    };

    private static string GetSql(string provider, SqlOperationKind operation) =>
        provider != SqliteSqlProvider.Instance.Key ? null : operation switch
        {
            SqlOperationKind.InsertValues => InsertSql,
            SqlOperationKind.Update => UpdateSql,
            SqlOperationKind.Delete => DeleteSql,
            _ => null
        };

    private sealed class ReturningRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Version { get; set; }
    }
}
