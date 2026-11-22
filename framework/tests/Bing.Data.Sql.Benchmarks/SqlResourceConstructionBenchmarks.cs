using BenchmarkDotNet.Attributes;
using Bing.Data;
using Bing.Data.Sql;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>单独测量实际执行入口创建和无 I/O 释放的资源分配。</summary>
[MemoryDiagnoser]
public class SqlResourceConstructionBenchmarks
{
    private ServiceProvider _services = null!;
    private readonly SqlOptions _options = new();

    [GlobalSetup]
    public void Setup()
    {
        _services = new ServiceCollection().BuildServiceProvider();
        Query();
        Writer();
        Multiple();
    }

    /// <summary>查询入口构造及未使用资源的释放。</summary>
    [Benchmark]
    public void Query() { using var query = new QueryEntry(_services, _options); }

    /// <summary>写入入口构造及未使用资源的释放。</summary>
    [Benchmark]
    public void Writer() { using var writer = new WriteEntry(_services, _options); }

    /// <summary>多结果入口构造及未使用资源的释放。</summary>
    [Benchmark]
    public void Multiple() { using var multiple = new MultipleEntry(_services, _options); }

    [GlobalCleanup]
    public void Cleanup() => _services.Dispose();

    private sealed class QueryEntry(IServiceProvider services, SqlOptions options) : SqlQueryBase(services, options);
    private sealed class WriteEntry(IServiceProvider services, SqlOptions options) : SqlExecutorBase(services, options);
    private sealed class MultipleEntry(IServiceProvider services, SqlOptions options) : SqlMultipleQueryExecutorBase(services, options);
}
