using System.Linq.Expressions;
using Bing.Data.Sql;

internal static class CachedPredicateSmoke
{
    private static readonly Expression<Func<Row, bool>> Visible = row => row.Version >= 1;
    private static readonly Expression<Func<Row, long, bool>> Minimum = (row, value) => row.Id >= value;
    private static readonly Expression<Func<Row, object>> Aggregate = row => new { Count = SqlFn.Count(row.Id) };
    private static readonly Expression<Func<Row, VersionSummary>> Grouped =
        row => new VersionSummary { Version = row.Version, Count = SqlFn.Count(row.Id) };
    private static readonly Expression<Func<Row, object>> Version = row => row.Version;
    private static readonly Expression<Func<Row, object>> Name = row => row.Name;

    internal static (string Sql, long[] Ids) Read(ISqlQueryFactory factory, bool enabled, long minimum)
    {
        using var root = factory.Create();
        var query = root.From<Row>("r")
            .Where<Row>(Visible, "r")
            .WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .Select<Row>(row => row.Id, "r");
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }

    internal static string TemplateSql(ISqlQueryFactory factory, bool enabled)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row>(enabled, Visible, "r")
            .Select<Row>(row => row.Id, "r").ToSql();
    }

    internal static string ExplicitTemplateSql(ISqlQueryFactory factory, bool enabled, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .Select<Row>(row => row.Id, "r").ToSql();
    }

    internal static long[] ExplicitTemplateIds(ISqlQueryFactory factory, bool enabled, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .Select<Row>(row => row.Id, "r").ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray();
    }

    internal static string RequiredTemplateSql(ISqlQueryFactory factory, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").Where<Row, long>(minimum, Minimum, "r")
            .Select<Row>(row => row.Id, "r").ToSql();
    }

    internal static long[] RequiredTemplateIds(ISqlQueryFactory factory, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").Where<Row, long>(minimum, Minimum, "r")
            .Select<Row>(row => row.Id, "r").ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray();
    }

    internal static string AggregateTemplateSql(ISqlQueryFactory factory, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(true, minimum, Minimum, "r")
            .Select<Row>(Aggregate, "r").ToSql();
    }

    internal static long AggregateTemplateCount(ISqlQueryFactory factory, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(true, minimum, Minimum, "r")
            .Select<Row>(Aggregate, "r")
            .ToList<VersionSummary>().Single().Count;
    }

    internal static string GroupedSql(ISqlQueryFactory factory, bool enabled, long minimum, bool descending)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .Select<Row, VersionSummary>(Grouped, "r").GroupBy<Row>(Version, "r")
            .OrderBy<Row>(Version, descending, "r").ToSql();
    }

    internal static VersionSummary[] GroupedRows(ISqlQueryFactory factory, bool enabled, long minimum, bool descending)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .Select<Row, VersionSummary>(Grouped, "r").GroupBy<Row>(Version, "r")
            .OrderBy<Row>(Version, descending, "r").ToList<VersionSummary>().ToArray();
    }

    internal static (string Sql, long[] Ids) ExcludedRows(ISqlQueryFactory factory)
    {
        using var root = factory.Create();
        var query = root.From<Row>("r").SelectAll().ExcludeSelect<Row>(Name, "r");
        return (query.ToSql(), query.ToList<Row>().Select(row => row.Id).OrderBy(id => id).ToArray());
    }

    internal static string ExcludedTemplateSql(ISqlQueryFactory factory, bool enabled, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .SelectAll().ExcludeSelect<Row>(Name, "r").ToSql();
    }

    internal static Row[] ExcludedTemplateRows(ISqlQueryFactory factory, bool enabled, long minimum)
    {
        using var root = factory.Create();
        return root.From<Row>("r").WhereIf<Row, long>(enabled, minimum, Minimum, "r")
            .SelectAll().ExcludeSelect<Row>(Name, "r").ToList<Row>().OrderBy(row => row.Id).ToArray();
    }
}
