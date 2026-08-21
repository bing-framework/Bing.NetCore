using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Bing.Data.Sql.Benchmarks;

internal sealed class SqlLambdaBenchmarkConfig : ManualConfig
{
    public SqlLambdaBenchmarkConfig() => AddColumn(new Gen2CollectionsColumn());
}

internal sealed class Gen2CollectionsColumn : IColumn
{
    public string Id => nameof(Gen2CollectionsColumn);
    public string ColumnName => "Gen2";
    public string Legend => "Number of Gen2 garbage collections per 1,000 operations";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Metric;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
        GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        return report?.GcStats is { } stats
            ? stats.Gen2Collections.ToString("0.####", CultureInfo.InvariantCulture)
            : "0";
    }

    public bool IsAvailable(Summary summary) => summary.Reports.Length > 0;

    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => true;

    public bool IsBaseline(Summary summary, BenchmarkCase benchmarkCase) => false;
}