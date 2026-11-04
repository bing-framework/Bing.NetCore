using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;

namespace Bing.Ddd.Domain.Benchmarks;

/// <summary>等字段、等输出比较旧表达式读取与生成直接值入口。</summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(5)]
public class LegacyDirectChangeBenchmarks
{
    private readonly LegacyMeasureEntity _before = new() { Name = "old", Code = "before" };
    private readonly LegacyMeasureEntity _after = new() { Name = "new", Code = "after" };
    private readonly ChangeTrackingContext _context = new();

    /// <summary>先核对完整输出，避免比较不同业务工作量。</summary>
    [GlobalSetup]
    public void Setup()
    {
        var direct = string.Join("|", Direct().Select(c => c.PropertyName+":"+c.Description+":"+c.OldValue+":"+c.NewValue));
        var expression = string.Join("|", Expression().Select(c => c.PropertyName+":"+c.Description+":"+c.OldValue+":"+c.NewValue));
        if (direct != expression || Direct().Count() != 2)
            throw new InvalidOperationException("测量工作负载输出不一致。");
    }

    [Benchmark(Baseline = true)]
    public ChangedValueDescriptorCollection Expression()
    {
        _context.FlushCache();
        _context.Add<LegacyMeasureEntity,string,string>(v=>v.Code, _before, _after.Code);
        _context.Add<LegacyMeasureEntity,string,string>(v=>v.Name, _before, _after.Name);
        return _context.GetChangedValueDescriptor();
    }

    [Benchmark]
    public ChangedValueDescriptorCollection Direct() => _before.GetChanges(_after);
}

/// <summary>源生成器负责生成真实直接调用，不手写性能替代实现。</summary>
public partial class LegacyMeasureEntity : DomainObjectBase<LegacyMeasureEntity>
{
    public string Name { get; set; }
    public string Code { get; set; }
}
