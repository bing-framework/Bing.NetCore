using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Bing.Domain.ChangeTracking;

namespace Bing.Ddd.Domain.Benchmarks;

/// <summary>
/// 测量订单及其明细通过生成代码执行的变更比较。
/// </summary>
[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(7)]
public class OrderChangeTrackingBenchmarks
{
    private global::Order _before = null!;
    private global::Order _after = null!;

    /// <summary>
    /// 获取或设置参与比较的订单明细数量。
    /// </summary>
    [Params(2, 100, 1000)]
    public int LineCount { get; set; }

    /// <summary>
    /// 在计时开始前构造独立订单快照并校验比较结果。
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _before = BuildOrder(LineCount, isAfter: false);
        _after = BuildOrder(LineCount, isAfter: true);

        var actual = _before.GetChanges(_after)
            .Select(change => (change.PropertyName, change.Description, change.OldValue, change.NewValue))
            .OrderBy(change => change.PropertyName, StringComparer.Ordinal)
            .ToArray();

        var lastLineId = 1000 + LineCount;
        var addedLineId = lastLineId + 1;
        var expected = new[]
            {
                ("Address.City", "城市", "\"上海\"", "\"杭州\""),
                ($"Lines[1001].Quantity", "数量", "2", "5"),
                ($"Lines[{lastLineId}]", "订单明细", "<object>", "<missing>"),
                ($"Lines[{addedLineId}]", "订单明细", "<missing>", "<object>"),
                ("Status", "订单状态", "\"待处理\"", "\"已确认\"")
            }
            .OrderBy(change => change.Item1, StringComparer.Ordinal)
            .ToArray();

        if (!actual.SequenceEqual(expected))
        {
            var actualText = string.Join(Environment.NewLine, actual.Select(FormatChange));
            var expectedText = string.Join(Environment.NewLine, expected.Select(FormatChange));
            throw new InvalidOperationException(
                $"订单变更基准工作负载与预期不符。{Environment.NewLine}实际：{Environment.NewLine}{actualText}{Environment.NewLine}预期：{Environment.NewLine}{expectedText}");
        }
    }

    /// <summary>
    /// 比较两份独立订单快照。
    /// </summary>
    /// <returns>订单属性及明细的变更描述。</returns>
    [Benchmark]
    public ChangedValueDescriptorCollection GetChanges() => _before.GetChanges(_after);

    private static global::Order BuildOrder(int lineCount, bool isAfter)
    {
        var order = new global::Order
        {
            Id = 1,
            Status = isAfter ? "已确认" : "待处理",
            Address = new global::Address { City = isAfter ? "杭州" : "上海" }
        };

        for (var index = 0; index < lineCount; index++)
        {
            var isLastLine = index == lineCount - 1;
            if (isAfter && isLastLine)
                continue;

            order.Lines.Add(new global::OrderLine
            {
                Id = 1001 + index,
                Quantity = index == 0 && isAfter ? 5 : index == 0 ? 2 : 1
            });
        }

        if (isAfter)
        {
            order.Lines.Add(new global::OrderLine
            {
                Id = 1001 + lineCount,
                Quantity = 3
            });
        }

        return order;
    }

    private static string FormatChange((string Path, string Description, string OldValue, string NewValue) change) =>
        $"{change.Path} | {change.Description} | {change.OldValue} -> {change.NewValue}";
}
