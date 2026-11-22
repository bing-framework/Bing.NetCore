using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>同一候选版本中比较生成访问与兼容访问；不是历史版本的 before/after。</summary>
[MemoryDiagnoser]
public class SqlGeneratedAccessBenchmarks
{
    private readonly Row _row = new() { Id = 7 };
    private readonly Row _derivedRow = new DerivedRow { Id = 7 };
    private readonly ColumnMappingMetadata _column = new() { PropertyName = nameof(Row.Id) };
    private readonly RegisteredEntityModelMetadataProvider _models = new(new EntityModelMetadata(typeof(Row), "rows", null,
        new[] { EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
            generatedGetter: value => ((Row)value).Id, generatedSetter: (value, id) => ((Row)value).Id = (int)id) }, () => new Row(),
        (value, propertyName) => propertyName == nameof(Row.Id) ? ((Row)value).Id : null));
    private SqlMutationPlanCache _compatible;
    private SqlMutationPlanCache _generated;
    private SqlMutationPlan _legacyGeneratedPlan;
    private SqlMutationPlan _generatedPlan;
    private SqlFluentQuery _frozen;

    [GlobalSetup]
    public void Setup()
    {
        _compatible = new SqlMutationPlanCache();
        _generated = new SqlMutationPlanCache(models: _models, generatedOnly: true);
        _compatible.GetValue(_row, _column);
        _generated.GetValue(_row, _column);
        _generatedPlan = SqlMutationPlan.Create(new EntityMappingMetadata
        {
            EntityType = typeof(Row),
            Model = _models.GetMetadata<Row>(),
            Table = new SqlTableReference { TableName = "rows" },
            Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(Row.Id)] = new ColumnMappingMetadata
                {
                    PropertyName = nameof(Row.Id),
                    ColumnName = "Id",
                    ClrType = typeof(int),
                    IsKey = true,
                    CanUpdate = true
                }
            }
        }, SqlMutationOperation.Update, null, null);
        _legacyGeneratedPlan = SqlMutationPlan.Create(new EntityMappingMetadata
        {
            EntityType = typeof(Row),
            Model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
            {
                EntityPropertyMetadata.CreateGenerated(nameof(Row.Id), typeof(int), true, true,
                    isKey: true, generatedGetter: value => ((Row)value).Id)
            }, () => new Row()),
            Table = new SqlTableReference { TableName = "rows" },
            Columns = new Dictionary<string, ColumnMappingMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(Row.Id)] = new ColumnMappingMetadata
                {
                    PropertyName = nameof(Row.Id),
                    ColumnName = "Id",
                    ClrType = typeof(int),
                    IsKey = true,
                    CanUpdate = true
                }
            }
        }, SqlMutationOperation.Update, null, null);
        _generatedPlan.TryGetGeneratedValue(_row, _column, out _);
        if (!_generatedPlan.TryGetGeneratedValue(_derivedRow, _column, out var derivedValue) || !Equals(derivedValue, 7))
            throw new InvalidOperationException("Generated base getter did not accept the derived row.");
        var builder = new SqlServerBuilder();
        _frozen = SqlQueryRuntimeFactory.CreateQuery(builder.CreateQueryExecutorForBenchmark(), builder)
            .Select("Id").From("rows").Where("Id", 7);
        _frozen.ToList<Row>();
    }

    [Benchmark(Baseline = true)] public object CompatibleGetterWarm() => _compatible.GetValue(_row, _column);
    [Benchmark] public object GeneratedGetterWarm() => _generated.GetValue(_row, _column);
    [Benchmark] public object GeneratedPlanGetterWarm() =>
        _legacyGeneratedPlan.TryGetGeneratedValue(_row, _column, out var value) ? value : null;
    [Benchmark] public object GeneratedPlanValueReaderWarm() =>
        _generatedPlan.TryGetGeneratedValue(_row, _column, out var value) ? value : null;
    [Benchmark] public object GeneratedPlanDerivedGetterWarm() =>
        _generatedPlan.TryGetGeneratedValue(_derivedRow, _column, out var value) ? value : null;
    [Benchmark] public object CompatibleGetterCold() => new SqlMutationPlanCache().GetValue(_row, _column);
    [Benchmark] public object GeneratedGetterCold() => new SqlMutationPlanCache(models: _models, generatedOnly: true).GetValue(_row, _column);
    [Benchmark] public object FrozenQueryRepeatedExecution() => _frozen.ToList<Row>();
    [Benchmark] public string QueryCloneAndRender() => new SqlServerBuilder().Select("Id").From("rows").Where("Id", 7).Clone().ToSql();

    public class Row { public int Id { get; set; } }
    public sealed class DerivedRow : Row { }
}
