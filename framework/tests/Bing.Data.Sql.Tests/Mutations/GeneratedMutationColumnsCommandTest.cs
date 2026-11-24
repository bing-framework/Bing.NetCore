using System.Threading.Tasks;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Tests.Mutations;

/// <summary>多列筛选复用生成 SQL 片段，同时保留最终映射、绑定与并发合同。</summary>
public sealed class GeneratedMutationColumnsCommandTest
{
    private const string InsertSql = "Insert Into `filtered_rows` (`Name`, `Quantity`) Values (@_p_0, @_p_1)";
    private const string UpdateSql = "Update `filtered_rows` Set `Name` = @_p_0, `Quantity` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
    private const string Returning = " Returning `Id` As `Id`, `Name` As `Name`, `Quantity` As `Quantity`, `Version` As `Version`";

    /// <summary>包含与排除最终形成相同的有序写列，绑定仍使用本次实体及原始令牌。</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Update_FilteredColumns_ShouldUseGeneratedSqlAndOriginalToken(bool exclude, bool returning)
    {
        var calls = 0;
        var builder = CreateBuilder((provider, operation, properties, rows, returns) =>
        {
            calls++;
            Assert.Equal("bing.sqlite", provider);
            Assert.Equal(SqlOperationKind.Update, operation);
            Assert.Equal(1, rows);
            Assert.Equal(new[] { "Name", "Quantity" }, properties);
            Assert.Equal(returning, returns);
            return UpdateSql + (returns ? Returning : "");
        });
        var options = new SqlUpdateOptions<Row>
        {
            IncludeProperties = exclude ? null : new[] { "quantity", "NAME" },
            ExcludeProperties = exclude ? new[] { "Version" } : null
        }.Original(row => row.Version, 3);
        var row = new Row { Id = 7, Name = "changed", Quantity = 12, Version = 4 };
        var command = returning ? builder.UpdateReturning(row, options) : builder.Update(row, options);

        Assert.Equal(UpdateSql + (returning ? Returning : ""), command.Sql);
        Assert.Equal(new object[] { "changed", 12, 7, 3 }, command.Parameters.Select(p => p.Value));
        Assert.Equal(1, calls);
        Assert.Equal(returning, command.HasReturning);
        Assert.True(command.IsGeneratedExecution);
        Assert.True(command.ValidateAffectedRows);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Insert_FilteredColumns_ShouldGenerateSqlAndReturnWholeRow(bool returning)
    {
        var calls = 0;
        var builder = CreateBuilder((provider, operation, properties, rows, returns) =>
        {
            calls++;
            Assert.Equal(SqlOperationKind.InsertValues, operation);
            Assert.Equal(1, rows);
            Assert.Equal(new[] { "Name", "Quantity" }, properties);
            return InsertSql + (returns ? Returning : "");
        });
        var options = new SqlInsertOptions { IncludeProperties = new[] { "Quantity", "Name" } };
        var row = new Row { Name = "new", Quantity = 9 };
        var command = returning ? builder.InsertReturning(row, options) : builder.Insert(row, options);

        Assert.Equal(InsertSql + (returning ? Returning : ""), command.Sql);
        Assert.Equal(new object[] { "new", 9 }, command.Parameters.Select(p => p.Value));
        Assert.Equal(1, calls);
        Assert.True(command.IsGeneratedExecution);
    }

    /// <summary>修改最终映射后必须跳过生成工厂，继续使用动态映射的完整 SQL。</summary>
    [Theory]
    [InlineData("renamed_rows", null, "Update `renamed_rows` Set `Name` = @_p_0, `Quantity` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3")]
    [InlineData(null, "display_name", "Update `filtered_rows` Set `display_name` = @_p_0, `Quantity` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3")]
    public void Update_WhenMappingChanges_ShouldUseFinalMapping(string table, string column, string expected)
    {
        var calls = 0;
        var builder = CreateBuilder((_, _, _, _, _) => { calls++; return UpdateSql; }, table, column);
        var command = builder.Update(new Row { Id = 1, Name = "mapped", Quantity = 2, Version = 3 },
            new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Quantity" } });
        Assert.Equal(expected, command.Sql);
        Assert.Equal(0, calls);
        Assert.Equal(new object[] { "mapped", 2, 1, 3 }, command.Parameters.Select(p => p.Value));
    }

    [Fact]
    public void InsertCombined_WhenFactoryDeclines_ShouldRenderBuilderSql()
    {
        var builder = CreateBuilder((_, _, _, rows, _) => { Assert.Equal(2, rows); return null; });
        var command = builder.InsertCombined(new[] { new Row { Name = "a", Quantity = 2 }, new Row { Name = "b", Quantity = 3 } },
            new SqlInsertOptions { IncludeProperties = new[] { "Name", "Quantity" } });
        Assert.Equal(InsertSql + ", (@_p_2, @_p_3)", command.Sql);
        Assert.Equal(new object[] { "a", 2, "b", 3 }, command.Parameters.Select(p => p.Value));
    }

    [Fact]
    public void Update_WhenFactoryDeclines_ShouldRenderBuilderSql()
    {
        var builder = CreateBuilder((_, _, _, _, _) => null);
        var command = builder.Update(new Row { Id = 1, Name = "fallback", Quantity = 2, Version = 3 },
            new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Quantity" } });
        Assert.Equal(UpdateSql, command.Sql);
        Assert.Equal(new object[] { "fallback", 2, 1, 3 }, command.Parameters.Select(p => p.Value));
    }

    /// <summary>同一 Builder 的缓存命中和不同列形状不能混用列布局。</summary>
    [Fact]
    public void Update_WhenShapeChanges_ShouldIsolateCachedColumnLayouts()
    {
        var layouts = new List<string>();
        var builder = CreateBuilder((_, _, properties, _, _) =>
        {
            var layout = string.Join(",", properties);
            layouts.Add(layout);
            return layout == "Name,Quantity" ? UpdateSql :
                "Update `filtered_rows` Set `Quantity` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3";
        });
        var row = new Row { Id = 7, Name = "layout", Quantity = 8, Version = 3 };
        var first = builder.Update(row, new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Quantity" } });
        var second = builder.Update(row, new SqlUpdateOptions { IncludeProperties = new[] { "Quantity", "Version" } });
        var repeated = builder.Update(row, new SqlUpdateOptions { IncludeProperties = new[] { "quantity", "NAME" } });
        Assert.Equal(UpdateSql, first.Sql);
        Assert.Equal("Update `filtered_rows` Set `Quantity` = @_p_0, `Version` = @_p_1 Where `Id` = @_p_2 And `Version` = @_p_3", second.Sql);
        Assert.Equal(UpdateSql, repeated.Sql);
        Assert.Equal(new[] { "Name,Quantity", "Quantity,Version", "Name,Quantity" }, layouts);
        Assert.Equal(new object[] { 8, 3, 7, 3 }, second.Parameters.Select(p => p.Value));
    }

    /// <summary>写入数据边界仍必须由 Builder 添加，不能被生成片段绕过。</summary>
    [Fact]
    public void Update_WhenDataBoundaryApplies_ShouldKeepPredicateAndSkipColumnsFactory()
    {
        var builder = CreateBuilder((_, _, _, _, _) => throw new InvalidOperationException("boundary must use builder"),
            filters: new ISqlFilter[] { new QuantityBoundary() });
        var command = builder.Update(new Row { Id = 7, Name = "boundary", Quantity = 8, Version = 3 },
            new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Quantity" } });
        Assert.Equal(UpdateSql + " And `Quantity`=@_p_4", command.Sql);
        Assert.Equal(new object[] { "boundary", 8, 7, 3, 8 }, command.Parameters.Select(p => p.Value));
    }

    /// <summary>共享映射计划只保存列名，命令的参数快照不能跨请求共享。</summary>
    [Fact]
    public void Update_WhenBuiltConcurrently_ShouldIsolateParameters()
    {
        var builder = CreateBuilder((_, _, properties, _, _) =>
        {
            Assert.Equal(new[] { "Name", "Quantity" }, properties);
            return UpdateSql;
        });
        var commands = new SqlWriteCommand[24];
        Parallel.For(0, commands.Length, i => commands[i] = builder.Update(
            new Row { Id = i, Name = $"row-{i}", Quantity = i + 10, Version = i + 20 },
            new SqlUpdateOptions { IncludeProperties = new[] { "Name", "Quantity" } }));
        for (var i = 0; i < commands.Length; i++)
        {
            Assert.Equal(UpdateSql, commands[i].Sql);
            Assert.Equal(new object[] { $"row-{i}", i + 10, i, i + 20 }, commands[i].Parameters.Select(p => p.Value));
        }
    }

    private static DefaultSqlEntityMutationCommandBuilder CreateBuilder(
        Func<string, SqlOperationKind, IReadOnlyList<string>, int, bool, string> sql, string table = null, string column = null,
        IEnumerable<ISqlFilter> filters = null)
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
            new SqlBuilderServices(metadataOptions: metadata, entityModelMetadataProvider: new RegisteredEntityModelMetadataProvider(model), filters: filters));
    }

    private sealed class QuantityBoundary : ISqlFilter, ISqlDataBoundaryContributor
    {
        public void Filter(SqlFilterContext context) { }
        public bool ShouldApply(SqlDataBoundaryContext context) => context.EntityType == typeof(Row);
        public void Apply(SqlDataBoundaryContext context) => context.AddEquals("Quantity", 8);
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public int Version { get; set; }
    }
}
