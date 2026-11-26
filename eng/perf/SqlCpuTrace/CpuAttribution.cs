using System.Globalization;
using Microsoft.Diagnostics.Tracing.EventPipe;
using System.Text;

/// <summary>仅汇总目标进程的托管执行态采样；权重为样本数，不推算实际 CPU 毫秒。</summary>
internal sealed class CpuAttribution(int processId)
{
    internal sealed class MethodCount
    {
        public long SelfSamples { get; set; }
        public long InclusiveSamples { get; set; }
    }
    internal long ManagedSamples { get; private set; }
    internal long ExternalSamples { get; private set; }
    internal long OtherSamples { get; private set; }
    internal long MissingStackSamples { get; private set; }
    internal long UnresolvedStackSamples { get; private set; }
    internal long SqlStackSamples { get; private set; }
    internal Dictionary<string, MethodCount> Methods { get; } = new(StringComparer.Ordinal);
    internal CpuComponentAttribution Components { get; } = new();

    /// <summary>frames 按叶到根排列；递归方法的包含权重在同一样本内只计一次。</summary>
    internal void Add(int sampledProcessId, ClrThreadSampleType kind, string[] frames)
    {
        if (sampledProcessId != processId) return;
        if (kind == ClrThreadSampleType.External) { ExternalSamples++; return; }
        if (kind != ClrThreadSampleType.Managed) { OtherSamples++; return; }
        ManagedSamples++;
        if (frames.Length == 0) { MissingStackSamples++; frames = ["[missing stack]"]; }
        Components.Add(frames);
        if (frames.Any(x => x.StartsWith("[unresolved", StringComparison.Ordinal))) UnresolvedStackSamples++;
        if (frames.Any(x => x.StartsWith("Bing.Data.Sql.", StringComparison.Ordinal) &&
            !x.StartsWith("Bing.Data.Sql.Benchmarks.", StringComparison.Ordinal))) SqlStackSamples++;
        foreach (var frame in frames.Distinct(StringComparer.Ordinal))
        {
            if (!Methods.TryGetValue(frame, out var count)) Methods[frame] = count = new();
            count.InclusiveSamples++;
        }
        Methods[frames[0]].SelfSamples++;
    }

    internal bool IsVerified(long lostEvents) => ManagedSamples > 0 && SqlStackSamples > 0 && lostEvents == 0;
    internal string ToCsv()
    {
        var csv = new StringBuilder("Method,SelfSamples,InclusiveSamples,SelfPercent,InclusivePercent\n");
        foreach (var item in Methods.OrderByDescending(x => x.Value.SelfSamples).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            csv.Append('"').Append(item.Key.Replace("\"", "\"\"", StringComparison.Ordinal)).Append("\",");
            csv.Append(item.Value.SelfSamples.ToString(CultureInfo.InvariantCulture)).Append(',');
            csv.Append(item.Value.InclusiveSamples.ToString(CultureInfo.InvariantCulture)).Append(',');
            csv.Append((100d * item.Value.SelfSamples / ManagedSamples).ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
            csv.Append((100d * item.Value.InclusiveSamples / ManagedSamples).ToString("0.######", CultureInfo.InvariantCulture)).Append('\n');
        }
        return csv.ToString();
    }
}
