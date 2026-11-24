using System.Threading.Tasks;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>多行筛选列复用生成 SQL，并按行绑定独立请求参数。</summary>
public sealed class GeneratedMutationRowsCommandTest
{
    private const string Head = "Insert Into `filtered_rows` (`Name`, `Quantity`) Values ";
    private const string Row1 = "(@_p_0, @_p_1)";
    private const string Row2 = ", (@_p_2, @_p_3)";
    private const string Row3 = ", (@_p_4, @_p_5)";

    /// <summary>Include/Exclude 只固定列布局，行数及实体值来自每次调用。</summary>
    [Theory]
    [InlineData(false, 1, Head + Row1)]
    [InlineData(false, 2, Head + Row1 + Row2)]
    [InlineData(false, 3, Head + Row1 + Row2 + Row3)]
    [InlineData(true, 1, Head + Row1)]
    [InlineData(true, 2, Head + Row1 + Row2)]
    [InlineData(true, 3, Head + Row1 + Row2 + Row3)]
    public void InsertCombined_WithFilteredColumns_ShouldUseRowMajorSql(bool exclude, int count, string expected)
    {
        var calls = 0;
        var builder = CreateBuilder((provider, operation, columns, rows, returning) =>
        {
            calls++;
            Assert.Equal("bing.sqlite", provider);
            Assert.Equal(SqlOperationKind.InsertValues, operation);
            Assert.Equal(new[] { "Name", "Quantity" }, columns);
            Assert.Equal(count, rows);
            Assert.False(returning);
            return expected;
        });
        var entities = Rows(count);
        var command = builder.InsertCombined(entities, Options(exclude));

        Assert.Equal(expected, command.Sql);
        Assert.Equal(1, calls);
        Assert.Equal(entities.SelectMany(row => new object[] { row.Name, row.Quantity }),
            command.Parameters.Select(parameter => parameter.Value));
        Assert.Equal(Enumerable.Range(0, count * 2).Select(index => "@_p_" + index),
            command.Parameters.Select(parameter => parameter.Name));
        Assert.True(command.IsGeneratedExecution);
        Assert.NotNull(command.GeneratedParameterBinder);
        Assert.False(command.HasReturning);
        Assert.False(command.ValidateAffectedRows);

        entities[0].Name = "later";
        command.Parameters.First().Value = "exposed";
        Assert.Equal("row-1", command.CreateParameters().First().Value);
    }

    /// <summary>映射覆盖不能绕过最终表名或列名；回退仍保持静态参数绑定。</summary>
    [Theory]
    [InlineData("renamed_rows", null,
        "Insert Into `renamed_rows` (`Name`, `Quantity`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    [InlineData(null, "display_name",
        "Insert Into `filtered_rows` (`display_name`, `Quantity`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)")]
    public void InsertCombined_WhenMappingChanges_ShouldUseFinalMapping(string table, string column, string expected)
    {
        var builder = CreateBuilder((_, _, _, _, _) => throw new InvalidOperationException("mapped SQL must use Builder"),
            table, column);
        var command = builder.InsertCombined(Rows(2), Options(false));

        Assert.Equal(expected, command.Sql);
        Assert.Equal(new object[] { "row-1", 11, "row-2", 12 }, command.Parameters.Select(parameter => parameter.Value));
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>可选生成器不覆盖某个形状时继续使用现有 Builder。</summary>
    [Fact]
    public void InsertCombined_WhenColumnsFactoryDeclines_ShouldRenderBuilderSql()
    {
        var calls = 0;
        var builder = CreateBuilder((_, _, _, _, _) => { calls++; return null; });
        var command = builder.InsertCombined(Rows(3), Options(false));

        Assert.Equal(Head + Row1 + Row2 + Row3, command.Sql);
        Assert.Equal(1, calls);
        Assert.Equal(new object[] { "row-1", 11, "row-2", 12, "row-3", 13 },
            command.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>缓存命中时行数仍按调用传入，不同选列布局独立保存。</summary>
    [Fact]
    public void InsertCombined_WhenShapeChanges_ShouldReuseLayoutAndCurrentRowCount()
    {
        var calls = new List<(IReadOnlyList<string> Columns, int Rows)>();
        const string otherSql = "Insert Into `filtered_rows` (`Quantity`, `Version`) Values (@_p_0, @_p_1), (@_p_2, @_p_3)";
        var builder = CreateBuilder((_, _, columns, rows, _) =>
        {
            calls.Add((columns, rows));
            return columns[0] == "Quantity" ? otherSql : Head + Row1 + Row2 + (rows == 3 ? Row3 : "");
        });
        var first = builder.InsertCombined(Rows(2), Options(false));
        var second = builder.InsertCombined(Rows(3), Options(false));
        var third = builder.InsertCombined(Rows(2), new SqlInsertOptions { IncludeProperties = new[] { "Version", "Quantity" } });

        Assert.Equal(Head + Row1 + Row2, first.Sql);
        Assert.Equal(Head + Row1 + Row2 + Row3, second.Sql);
        Assert.Equal(otherSql, third.Sql);
        Assert.Equal(new[] { 2, 3, 2 }, calls.Select(call => call.Rows));
        Assert.Same(calls[0].Columns, calls[1].Columns);
        Assert.NotSame(calls[0].Columns, calls[2].Columns);
        Assert.Equal(new object[] { 11, 21, 12, 22 }, third.Parameters.Select(parameter => parameter.Value));
    }

    /// <summary>同一映射计划可共享，每批参数和请求实体仍各自独立。</summary>
    [Fact]
    public void InsertCombined_WhenConcurrent_ShouldIsolateParameters()
    {
        var builder = CreateBuilder((_, _, columns, count, _) =>
        {
            Assert.Equal(new[] { "Name", "Quantity" }, columns);
            Assert.Equal(2, count);
            return Head + Row1 + Row2;
        });
        Parallel.For(0, 24, index =>
        {
            var command = builder.InsertCombined(new[]
            {
                new Row { Name = "first-" + index, Quantity = index },
                new Row { Name = "second-" + index, Quantity = index + 1 }
            }, Options(false));

            Assert.Equal(Head + Row1 + Row2, command.Sql);
            Assert.Equal(new object[] { "first-" + index, index, "second-" + index, index + 1 },
                command.Parameters.Select(parameter => parameter.Value));
        });
    }

    private static SqlInsertOptions Options(bool exclude) => new()
    {
        IncludeProperties = exclude ? null : new[] { "quantity", "NAME" },
        ExcludeProperties = exclude ? new[] { "Id", "Version" } : null
    };

    private static Row[] Rows(int count) => Enumerable.Range(1, count)
        .Select(index => new Row { Id = index, Name = "row-" + index, Quantity = index + 10, Version = index + 20 }).ToArray();

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(
        Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string> sql, string table = null, string column = null)
    {
        var model = new EntityModelMetadata(typeof(Row), "filtered_rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true, isKey: true, generatedGetter: source => ((Row)source).Id),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true, generatedGetter: source => ((Row)source).Name),
            EntityPropertyMetadata.CreateGenerated("Quantity", typeof(int), true, true, generatedGetter: source => ((Row)source).Quantity),
            EntityPropertyMetadata.CreateGenerated("Version", typeof(int), true, true, isConcurrencyToken: true, generatedGetter: source => ((Row)source).Version)
        }) { GeneratedMutationColumnsSql = sql };
        var metadata = new SqlMetadataOptions { ExecutionMode = SqlExecutionMode.GeneratedOnly };
        var mapping = new EntityMappingOptions { EntityType = typeof(Row), TableName = table ?? model.TableName };
        if (column != null)
            mapping.Columns["Name"] = new ColumnMappingOptions { PropertyName = "Name", ColumnName = column };
        metadata.EntityMappings.Add(mapping);
        return new DefaultSqlEntityMutationCommandBuilder(SqliteSqlProvider.Instance,
            new SqlBuilderServices(metadataOptions: metadata, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model)));
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int Version { get; set; }
    }
}
