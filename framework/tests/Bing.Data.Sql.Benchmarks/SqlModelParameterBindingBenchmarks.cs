using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>测量真实默认绑定器的模型和字典绑定，包含上下文快照，不包含命令与数据库。</summary>
[MemoryDiagnoser]
public class SqlModelParameterBindingBenchmarks
{
    private DefaultSqlParameterBinder _binder;
    private Row _row;
    private Dictionary<string, object> _dictionary;

    [Params(1, 8, 32)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _row = new Row { Values = Enumerable.Range(1, Count).ToArray() };
        _dictionary = new Dictionary<string, object>();
        var properties = new EntityPropertyMetadata[Count];
        for (var index = 0; index < Count; index++)
        {
            var slot = index;
            var name = "Value" + index;
            properties[index] = EntityPropertyMetadata.CreateGenerated(name, typeof(int), true, false,
                generatedGetter: source => ((Row)source).Values[slot]);
            _dictionary.Add(name, _row.Values[index]);
        }
        var models = new RegisteredEntityModelMetadataProvider(new EntityModelMetadata(typeof(Row), "filters", null, properties));
        var options = new SqlMetadataOptions
        {
            ExecutionMode = SqlExecutionMode.GeneratedOnly,
            DefaultDatabaseContext = new DatabaseContext { DbKey = "binding-benchmark", MappingProfile = "default" }
        };
        _binder = new DefaultSqlParameterBinder(new DefaultEntityMappingResolver(models, options: options), options: options, models: models);
        Validate(GeneratedModel());
        Validate(Dictionary());
    }

    [Benchmark]
    public object GeneratedModel() => _binder.Bind(_row);

    [Benchmark]
    public object Dictionary() => _binder.Bind(_dictionary);

    private void Validate(object bound)
    {
        var parameters = ((IDapperParameterSet)bound).Parameters;
        if (!parameters.Select(parameter => parameter.Name).SequenceEqual(_dictionary.Keys) ||
            !parameters.Select(parameter => parameter.Value).SequenceEqual(_dictionary.Values))
            throw new InvalidOperationException("Parameter names or values did not match the workload.");
    }

    private sealed class Row
    {
        public int[] Values { get; set; }
    }
}

/// <summary>测量兼容模式中未登记参数对象的绑定开销，不包含 Dapper 执行和数据库。</summary>
[MemoryDiagnoser]
public class SqlCompatibleUnregisteredParameterBindingBenchmarks
{
    private DefaultSqlParameterBinder _binder;
    private Filter _filter;

    [GlobalSetup]
    public void Setup()
    {
        _binder = new DefaultSqlParameterBinder(models: new CompositeEntityModelMetadataProvider());
        _filter = new Filter { Value = 17 };
        if (!ReferenceEquals(_filter, CompatibleUnregistered()))
            throw new InvalidOperationException("Unregistered parameters must keep the compatibility fallback.");
    }

    [Benchmark]
    public object CompatibleUnregistered() => _binder.Bind(_filter);

    private sealed class Filter { public int Value { get; set; } }
}
