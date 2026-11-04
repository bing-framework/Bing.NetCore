using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Domain.ChangeTracking;

namespace Bing.Ddd.Domain.Benchmarks;

/// <summary>
/// 真实生成模型的捕获、键匹配、循环共享引用及裁剪基准。
/// </summary>
[MemoryDiagnoser]
public class ChangeGraphBenchmarks
{
    /// <summary>
    /// 保存变更图跟踪基准使用的跟踪器。
    /// </summary>
    private ChangeGraphTracker _tracker;
    /// <summary>
    /// 保存变更图顺序移动基准使用的跟踪器。
    /// </summary>
    private ChangeGraphTracker _moves;
    /// <summary>
    /// 保存变更图基准使用的变更前订单。
    /// </summary>
    private Order _before;
    /// <summary>
    /// 保存变更图基准使用的变更后订单。
    /// </summary>
    private Order _after;
    /// <summary>
    /// 保存变更图基准使用的重排后订单。
    /// </summary>
    private Order _reordered;
    /// <summary>
    /// 保存变更图基准使用的仅忽略项订单。
    /// </summary>
    private readonly Order _ignoredOnly = new Order();
    /// <summary>
    /// 保存变更图基准使用的变更前快照。
    /// </summary>
    private ChangeGraphSnapshot<Order> _snapshot;

    /// <summary>
    /// 获取或设置稳定键列表的元素数量。
    /// </summary>
    [Params(100, 1000)]
    public int Count { get; set; }

    /// <summary>
    /// 准备变更图基准输入及冻结快照。
    /// </summary>
    /// <remarks>输入在计时区间之外创建；快照比较复用同一份未变的旧快照。</remarks>
    [GlobalSetup]
    public void Setup()
    {
        _tracker = new ChangeGraphTracker();
        _moves = new ChangeGraphTracker(ChangeGraphOptions.Default.WithTrackMoves());
        _before = Build(false);
        _after = Build(true);
        _reordered = Build(false);
        _reordered.Lines.Reverse();
        _snapshot = _tracker.Capture(_before);
    }

    /// <summary>
    /// 创建带共享循环引用的变更图基准模型。
    /// </summary>
    /// <param name="changed">是否构造包含变更的对象图。</param>
    /// <returns>带共享引用及循环结构的订单对象图。</returns>
    private Order Build(bool changed)
    {
        var address = new Address { City = changed ? "new" : "old" };
        address.Next = address;
        return new Order
        {
            Left = address,
            Right = address,
            Lines = Enumerable.Range(1, Count).Select(id => new Line { Id = id, Amount = changed && id == 1 ? 2 : 1 }).ToList(),
            Tags = new Dictionary<string, List<string>> { ["main"] = new List<string> { changed ? "new" : "old" } }
        };
    }

    /// <summary>
    /// 捕获完整的冻结对象图快照。
    /// </summary>
    /// <returns>与源对象后续修改隔离的订单图快照。</returns>
    /// <remarks>覆盖共享对象、循环引用及字典中嵌套列表。</remarks>
    [Benchmark]
    public ChangeGraphSnapshot<Order> Capture() => _tracker.Capture(_before);

    /// <summary>
    /// 比较两份独立对象图的叶节点变更。
    /// </summary>
    /// <returns>比较两份对象图产生的变更条目数。</returns>
    /// <remarks>固定输入应产生四条叶节点变更。</remarks>
    [Benchmark]
    public int Compare() => _tracker.Compare(_before, _after).Changes.Count;

    /// <summary>
    /// 比较已有快照与当前对象图。
    /// </summary>
    /// <returns>比较快照与当前图产生的变更条目数。</returns>
    [Benchmark]
    public int CompareSnapshot() => _tracker.CompareSnapshot(_snapshot, _after).Changes.Count;

    /// <summary>
    /// 检测稳定键列表反序后的元素移动。
    /// </summary>
    /// <returns>反序集合比较产生的变更条目数。</returns>
    /// <remarks>使用最长递增子序列移动检测路径。</remarks>
    [Benchmark]
    public int CompareMoves() => _moves.Compare(_before, _reordered).Changes.Count;

    /// <summary>
    /// 捕获忽略子树的对象图快照。
    /// </summary>
    /// <returns>与源对象后续修改隔离的订单图快照。</returns>
    /// <remarks>被忽略子树中的异常属性读取器不得执行。</remarks>
    [Benchmark]
    public ChangeGraphSnapshot<Order> CaptureIgnoredSubtree() => _tracker.Capture(_ignoredOnly);
}

/// <summary>
/// 由生产生成器配置的变更图基准订单模型。
/// </summary>
/// <remarks>
/// 根配置由生产源生成器处理。
/// </remarks>
public partial class Order : Bing.Domain.Entities.DomainObjectBase<Order>
{
    /// <summary>
    /// 获取或设置订单行列表。
    /// </summary>
    public List<Line> Lines { get; set; }
    /// <summary>
    /// 获取或设置左侧关联地址。
    /// </summary>
    public Address Left { get; set; }
    /// <summary>
    /// 获取或设置右侧关联地址。
    /// </summary>
    public Address Right { get; set; }
    /// <summary>
    /// 获取或设置按名称分组的标签列表。
    /// </summary>
    public Dictionary<string, List<string>> Tags { get; set; }
    /// <summary>
    /// 获取变更图捕获时应忽略的审计序列。
    /// </summary>
    [ChangeIgnore]
    public IEnumerable<Line> AuditTrail => throw new InvalidOperationException("Ignored getter must not run.");
}

/// <summary>
/// 表示变更图基准中的订单行节点。
/// </summary>
public class Line : Bing.Domain.Entities.IKey<int>
{
    /// <summary>
    /// 获取或设置记录标识。
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// 获取或设置测试使用的数值。
    /// </summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// 表示变更图基准中的地址节点。
/// </summary>
public class Address
{
    /// <summary>
    /// 获取或设置地址所在城市。
    /// </summary>
    public string City { get; set; }
    /// <summary>
    /// 获取或设置后续关联地址。
    /// </summary>
    public Address Next { get; set; }
}
