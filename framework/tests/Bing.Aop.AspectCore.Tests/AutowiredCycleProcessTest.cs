using System.Diagnostics;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 在独立进程中检查可能导致递归的属性注入。
/// </summary>
public class AutowiredCycleProcessTest
{
    /// <summary>
    /// 验证危险循环在有界进程中产生可诊断的结果。
    /// </summary>
    [Theory]
    [InlineData("property", "Stack overflow")]
    [InlineData("mixed", "Stack overflow")]
    [InlineData("factory", "Stack overflow")]
    [InlineData("lazy", "Stack overflow")]
    [InlineData("plain-factory", "bounded Microsoft DI factory recursion")]
    [InlineData("plain-lazy", "ValueFactory")]
    [InlineData("bing-lazy", "bounded Bing Lazier recursion")]
    public async Task ResolveCycle_ShouldRemainInsideBoundedChildProcess(string mode, string expectedMarker)
    {
        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "CycleProbe", "Bing.Aop.AspectCore.CycleProbe.dll");
        Assert.True(File.Exists(assemblyPath), $"Missing cycle probe: {assemblyPath}");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add(assemblyPath);
        process.StartInfo.ArgumentList.Add(mode);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exit = process.WaitForExitAsync();
        var completed = await Task.WhenAny(exit, Task.Delay(TimeSpan.FromSeconds(60)));
        if (completed != exit)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"Cycle probe timed out: {mode}");
        }

        var stdout = await output;
        var stderr = await error;
        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains(expectedMarker, stderr, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resolved:", stdout, StringComparison.Ordinal);
    }
}
