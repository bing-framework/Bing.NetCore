using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Tests.Samples;

namespace Bing.Data.Sql.Tests.Builders.Core;

/// <summary>列值共享时，别名绑定与集合修改只能作用于当前草稿。</summary>
public sealed class ColumnValueIsolationTest
{
    [Fact]
    public void Constructor_WhenInputListChanges_ShouldKeepOwnedColumns()
    {
        var input = new List<ColumnItem> { ColumnItem.CreateColumn("Id", "r") };
        var columns = new ColumnCollection(input);

        input.Clear();
        input.Add(ColumnItem.CreateColumn("Name", "other"));

        Assert.Equal("[r].[Id]", columns.ToSql(TestDialect.Instance, null));
        columns.Clear();
        Assert.Equal("Name", Assert.Single(input).Name);
    }

    [Fact]
    public void FreezeTableAlias_WhenItemIsShared_ShouldReplaceOnlyCurrentCollectionEntry()
    {
        var item = ColumnItem.CreateColumn("Id", tableType: typeof(Row));
        var first = new ColumnCollection(new List<ColumnItem> { item });
        var second = new ColumnCollection(new List<ColumnItem> { item });

        first.FreezeTableAlias(typeof(Row), "first");
        second.FreezeTableAlias(typeof(Row), "second");

        Assert.Null(item.TableAlias);
        Assert.Equal("[first].[Id]", first.ToSql(TestDialect.Instance, null));
        Assert.Equal("[second].[Id]", second.ToSql(TestDialect.Instance, null));
        Assert.NotSame(first[0], second[0]);
    }

    [Fact]
    public void Clone_ShouldShareImmutableItemsAndOwnItsCollection()
    {
        var columns = new ColumnCollection();
        columns.AddColumn(ColumnItem.CreateColumn("Id", tableType: typeof(Row)));
        columns.AddColumn(ColumnItem.CreateAggregate(SqlAggregateFunction.Sum, "Amount", columnAlias: "Total",
            distinct: true, databaseName: "archive", tableType: typeof(Row)));
        var clone = columns.Clone();

        Assert.Same(columns[0], clone[0]);
        Assert.Same(columns[1], clone[1]);
        clone.FreezeTableAlias(typeof(Row), "c");
        clone.AddColumns("Extra");

        Assert.Equal("[Id],Sum(Distinct [archive].[Amount]) As [Total]", columns.ToSql(TestDialect.Instance, null));
        Assert.Equal("[c].[Id],Sum(Distinct [archive].[c].[Amount]) As [Total],[Extra]",
            clone.ToSql(TestDialect.Instance, null));
        Assert.Equal(2, columns.Count);
    }

    [Fact]
    public void FreezeTableAlias_ShouldKeepExplicitAliasOtherTypesAndAggregateKinds()
    {
        var explicitAlias = ColumnItem.CreateColumn("Id", "explicit", tableType: typeof(Row));
        var otherType = ColumnItem.CreateColumn("Name", tableType: typeof(OtherRow));
        var columns = new ColumnCollection(new List<ColumnItem>
        {
            explicitAlias, otherType,
            ColumnItem.CreateAggregateWildcard(SqlAggregateFunction.Count, "Rows"),
            ColumnItem.CreateAggregateExpression(SqlAggregateFunction.Max, "[Amount] * 2", "Double"),
            ColumnItem.CreateAggregateRaw(SqlAggregateFunction.Min, "ABS(Amount)", "Minimum")
        });

        columns.FreezeTableAlias(typeof(Row), "r");

        Assert.Same(explicitAlias, columns[0]);
        Assert.Same(otherType, columns[1]);
        Assert.Equal("[explicit].[Id],[Name],Count(*) As [Rows],Max([Amount] * 2) As [Double],Min(ABS(Amount)) As [Minimum]",
            columns.ToSql(TestDialect.Instance, null));
    }

    [Fact]
    public void ColumnItem_ShouldExposeImmutableValuesAndReuseClone()
    {
        foreach (var name in new[] { nameof(ColumnItem.Name), nameof(ColumnItem.TableAlias), nameof(ColumnItem.ColumnAlias) })
            Assert.Null(typeof(ColumnItem).GetProperty(name)!.SetMethod);
        var item = ColumnItem.CreateRaw("COALESCE(Name, 'unknown')", "Display");

        Assert.Same(item, item.Clone());
        Assert.Equal("COALESCE(Name, 'unknown') As [Display]", item.ToSql(TestDialect.Instance, null));
    }

    [Fact]
    public void WithValues_ShouldPreserveAggregateMetadataAndSource()
    {
        var source = ColumnItem.CreateAggregate(SqlAggregateFunction.Sum, "Amount", "o", "Total",
            distinct: true, databaseName: "archive", tableType: typeof(Row));

        var changed = source.WithName("Price").WithTableAlias("p").WithColumnAlias("SumPrice");

        Assert.Equal("Sum(Distinct [archive].[o].[Amount]) As [Total]", source.ToSql(TestDialect.Instance, null));
        Assert.Equal("Sum(Distinct [archive].[p].[Price]) As [SumPrice]", changed.ToSql(TestDialect.Instance, null));
        Assert.Equal(typeof(Row), changed.TableType);
        Assert.False(changed.Raw);
        Assert.Equal(SqlAggregateFunction.Sum, changed.AggregateFunction);
        Assert.True(changed.AggregateDistinct);
        Assert.False(changed.AggregateWildcard);
        Assert.False(changed.AggregateArgumentRaw);
    }

    [Fact]
    public void WithValues_WhenUnchanged_ShouldReuseValueAndAllowClearingAliases()
    {
        var source = ColumnItem.CreateColumn("Id", "o", "Key", typeof(Row));

        Assert.Same(source, source.WithName("Id"));
        Assert.Same(source, source.WithTableAlias("o"));
        Assert.Same(source, source.WithColumnAlias("Key"));
        Assert.Equal("[Id]", source.WithTableAlias(null).WithColumnAlias(null).ToSql(TestDialect.Instance, null));
        Assert.Equal("[o].[Id] As [Key]", source.ToSql(TestDialect.Instance, null));
    }

    [Fact]
    public void WithValues_ShouldPreserveRawExpressionAndWildcardKinds()
    {
        var raw = ColumnItem.CreateRaw("COALESCE(Name, 'unknown')", "Name").WithColumnAlias("Display");
        var expression = ColumnItem.CreateAggregateExpression(SqlAggregateFunction.Max, "[Price] * 2", "Total",
            distinct: true).WithName("[Price] * 3").WithColumnAlias("Maximum");
        var wildcard = ColumnItem.CreateAggregateWildcard(SqlAggregateFunction.Count, "Rows").WithColumnAlias("Total");
        var aggregateRaw = ColumnItem.CreateAggregateRaw(SqlAggregateFunction.Min, "ABS(Price)", "Minimum")
            .WithTableAlias("ignored").WithColumnAlias("Smallest");

        Assert.True(raw.Raw);
        Assert.Equal("COALESCE(Name, 'unknown') As [Display]", raw.ToSql(TestDialect.Instance, null));
        Assert.True(expression.AggregateArgumentRaw);
        Assert.Equal("Max(Distinct [Price] * 3) As [Maximum]", expression.ToSql(TestDialect.Instance, null));
        Assert.True(wildcard.AggregateWildcard);
        Assert.Equal("Count(*) As [Total]", wildcard.ToSql(TestDialect.Instance, null));
        Assert.True(aggregateRaw.AggregateArgumentRaw);
        Assert.Equal("Min(ABS(Price)) As [Smallest]", aggregateRaw.ToSql(TestDialect.Instance, null));
    }

    private sealed class Row { }
    private sealed class OtherRow { }
}
