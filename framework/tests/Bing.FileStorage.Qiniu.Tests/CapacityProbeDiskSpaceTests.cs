using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Bing.FileStorage.CapacityProbe;
using Xunit;

namespace Bing.FileStorage.Qiniu.Tests;

/// <summary>
/// 覆盖相同键回源任务的协调等测试场景。
/// </summary>
public sealed class CapacityProbeDiskSpaceTests
{
    /// <summary>
    /// 定义容量探测使用的分片大小。
    /// </summary>
    private const int PartSize = 16 * 1024 * 1024;
    /// <summary>
    /// 定义容量换算使用的 GiB 字节数。
    /// </summary>
    private const long GiB = 1024L * 1024 * 1024;
    /// <summary>
    /// 定义指定其他卷根目录的环境变量名称。
    /// </summary>
    private const string OtherVolumeRootEnvironmentVariable = "BING_CAPACITY_TEST_OTHER_VOLUME_ROOT";
    /// <summary>
    /// 定义磁盘空间比较允许的字节误差。
    /// </summary>
    private const long DiskSpaceComparisonToleranceBytes = 64L * 1024 * 1024;

    /// <summary>
    /// 验证七牛容量探针按临时卷可用空间判断分片预算。
    /// </summary>
    [Fact]
    public void Qiniu_UsesTempVolume_WhenRootIsLowAndTempHasPartBudget()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-root"));
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-temp"));
        const string rootVolume = "root-volume";
        const string tempVolume = "temp-volume";
        CapacityDiskTarget? queried = null;

        var result = CapacityProbeDiskSpace.Measure("qiniu", root, temp, 100 * GiB, PartSize, 1, target =>
        {
            queried = target;
            return target.VolumeRoot == tempVolume ? PartSize : 0;
        }, directory => directory == root ? rootVolume : tempVolume);

        Assert.Equal(temp, result.DirectoryPath);
        Assert.Equal(temp, queried!.DirectoryPath);
        Assert.Equal(tempVolume, result.VolumeRoot);
        Assert.NotEqual(rootVolume, result.VolumeRoot);
        Assert.Equal(PartSize, result.RequiredBytes);
        Assert.True(result.HasEnoughSpace);
    }

    /// <summary>
    /// 验证根卷空间充足时仍拒绝临时卷空间不足。
    /// </summary>
    [Fact]
    public void Qiniu_RejectsLowTempVolume_EvenWhenRootHasPlentyOfSpace()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-root"));
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-temp"));
        const string rootVolume = "root-volume";
        const string tempVolume = "temp-volume";
        CapacityDiskTarget? queried = null;

        var result = CapacityProbeDiskSpace.Measure("qiniu", root, temp, 100 * GiB, PartSize, 1, target =>
        {
            queried = target;
            return target.VolumeRoot == rootVolume ? 1000 * GiB : PartSize - 1;
        }, directory => directory == root ? rootVolume : tempVolume);

        Assert.Equal(temp, result.DirectoryPath);
        Assert.Equal(temp, queried!.DirectoryPath);
        Assert.Equal(tempVolume, result.VolumeRoot);
        Assert.Equal(PartSize, result.RequiredBytes);
        Assert.False(result.HasEnoughSpace);
    }

    /// <summary>
    /// 验证远程后端按实际并发上传数预算分片空间。
    /// </summary>
    [Fact]
    public void RemoteBackend_BudgetsOnePartPerActualConcurrentUpload()
    {
        const int actualParallelism = 3;
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-temp"));

        var result = CapacityProbeDiskSpace.Measure("minio", "unused-root", temp, 100 * GiB, PartSize,
            actualParallelism, _ => PartSize * actualParallelism, _ => "test-temp-volume");

        Assert.Equal(PartSize * actualParallelism, result.RequiredBytes);
        Assert.True(result.HasEnoughSpace);
    }

    /// <summary>
    /// 验证本地后端按根卷计算并保留合并空间预算。
    /// </summary>
    [Fact]
    public void Local_UsesRootVolumeAndKeepsMergeCapacityBudget()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-root"));
        var temp = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "capacity-probe-temp"));
        CapacityDiskTarget? queried = null;
        var expected = 3 * GiB + 10 * GiB;

        var result = CapacityProbeDiskSpace.Measure("local", root, temp, GiB, PartSize, 1, target =>
        {
            queried = target;
            return expected;
        }, _ => "test-root-volume");

        Assert.Equal(root, result.DirectoryPath);
        Assert.Equal(root, queried!.DirectoryPath);
        Assert.Equal(expected, result.RequiredBytes);
        Assert.True(result.HasEnoughSpace);
    }

    /// <summary>
    /// 验证默认磁盘空间解析使用实际存在的目录。
    /// </summary>
    [Fact]
    public void DefaultResolution_UsesExistingDirectoryForVolumeAndSpaceQueries()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bing-capacity-ordinary-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            var result = CapacityProbeDiskSpace.Measure("local", directory, directory, 1, 1, 1);

            Assert.Equal(directory, result.DirectoryPath);
            Assert.False(string.IsNullOrWhiteSpace(result.VolumeRoot));

            if (OperatingSystem.IsWindows())
            {
                AssertWindowsVolumeMatches(directory, result.VolumeRoot);
                AssertSpaceMatches(result.AvailableBytes, QueryWindowsAvailableBytes(directory));
                AssertSpaceMatches(result.ReadAvailableBytes(), QueryWindowsAvailableBytes(directory));
            }
            else
            {
                AssertSpaceMatches(result.AvailableBytes, new DriveInfo(directory).AvailableFreeSpace);
                AssertSpaceMatches(result.ReadAvailableBytes(), new DriveInfo(directory).AvailableFreeSpace);
            }
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: false);
        }
    }

    /// <summary>
    /// 验证目录缺失时失败而不回退到路径根目录。
    /// </summary>
    [Fact]
    public void DefaultResolution_FailsForMissingDirectoryInsteadOfFallingBackToPathRoot()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "bing-capacity-missing-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missingDirectory));

        Assert.ThrowsAny<IOException>(() => CapacityProbeDiskSpace.Measure(
            "local", missingDirectory, Path.GetTempPath(), 1, 1, 1));
    }

    /// <summary>
    /// 验证容量预检及子目录采样使用目录联接的目标卷。
    /// </summary>
    [CrossVolumeJunctionFact]
    public void DefaultResolution_UsesJunctionTargetVolumeForPrecheckAndSubdirectorySampling()
    {
        var targetBase = Path.GetFullPath(Environment.GetEnvironmentVariable(OtherVolumeRootEnvironmentVariable)!);
        var sourceVolume = GetWindowsVolumePath(Path.GetFullPath(Path.GetTempPath()));
        var targetVolume = GetWindowsVolumePath(targetBase);
        Assert.NotEqual(sourceVolume, targetVolume, StringComparer.OrdinalIgnoreCase);

        var targetDirectory = Path.Combine(targetBase, "bing-capacity-target-" + Guid.NewGuid().ToString("N"));
        var junctionDirectory = Path.Combine(Path.GetTempPath(), "bing-capacity-junction-" + Guid.NewGuid().ToString("N"));
        var childDirectory = Path.Combine(junctionDirectory, "nested");
        Directory.CreateDirectory(targetDirectory);
        try
        {
            CreateJunction(junctionDirectory, targetDirectory);
            Directory.CreateDirectory(childDirectory);

            var rootSnapshot = CapacityProbeDiskSpace.Measure(
                "qiniu", targetDirectory, junctionDirectory, 1, 1, 1);
            Assert.Equal(Path.GetFullPath(junctionDirectory), rootSnapshot.DirectoryPath);
            Assert.Equal(targetVolume, rootSnapshot.VolumeRoot, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(sourceVolume, rootSnapshot.VolumeRoot, StringComparer.OrdinalIgnoreCase);
            AssertWindowsVolumeMatches(junctionDirectory, rootSnapshot.VolumeRoot);
            AssertSpaceMatches(rootSnapshot.AvailableBytes, QueryWindowsAvailableBytes(junctionDirectory));
            AssertSpaceMatches(rootSnapshot.ReadAvailableBytes(), QueryWindowsAvailableBytes(junctionDirectory));

            var childSnapshot = CapacityProbeDiskSpace.Measure(
                "qiniu", targetDirectory, childDirectory, 1, 1, 1);
            Assert.Equal(Path.GetFullPath(childDirectory), childSnapshot.DirectoryPath);
            Assert.Equal(targetVolume, childSnapshot.VolumeRoot, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual(sourceVolume, childSnapshot.VolumeRoot, StringComparer.OrdinalIgnoreCase);
            AssertWindowsVolumeMatches(childDirectory, childSnapshot.VolumeRoot);
            AssertSpaceMatches(childSnapshot.AvailableBytes, QueryWindowsAvailableBytes(childDirectory));
            AssertSpaceMatches(childSnapshot.ReadAvailableBytes(), QueryWindowsAvailableBytes(childDirectory));
        }
        finally
        {
            if (Directory.Exists(childDirectory)) Directory.Delete(childDirectory, recursive: false);
            if (Directory.Exists(junctionDirectory)) Directory.Delete(junctionDirectory, recursive: false);
            if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, recursive: false);
        }
    }

    /// <summary>
    /// 验证容量采样沿符号链接解析目标及嵌套目录。
    /// </summary>
    [UnixSymbolicLinkFact]
    public void DefaultResolution_FollowsSymbolicLinkForTargetAndNestedDirectorySampling()
    {
        var targetDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bing-capacity-real-" + Guid.NewGuid().ToString("N")));
        var linkDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "bing-capacity-link-" + Guid.NewGuid().ToString("N")));
        var targetChildDirectory = Path.Combine(targetDirectory, "nested");
        var linkChildDirectory = Path.Combine(linkDirectory, "nested");
        Directory.CreateDirectory(targetChildDirectory);
        try
        {
            Directory.CreateSymbolicLink(linkDirectory, targetDirectory);

            var targetSnapshot = CapacityProbeDiskSpace.Measure(
                "local", targetDirectory, targetDirectory, 1, 1, 1);
            var linkSnapshot = CapacityProbeDiskSpace.Measure(
                "local", linkDirectory, linkDirectory, 1, 1, 1);
            var targetChildSnapshot = CapacityProbeDiskSpace.Measure(
                "local", targetChildDirectory, targetChildDirectory, 1, 1, 1);
            var linkChildSnapshot = CapacityProbeDiskSpace.Measure(
                "local", linkChildDirectory, linkChildDirectory, 1, 1, 1);

            Assert.Equal(targetSnapshot.VolumeRoot, linkSnapshot.VolumeRoot, StringComparer.Ordinal);
            Assert.Equal(targetChildSnapshot.VolumeRoot, linkChildSnapshot.VolumeRoot, StringComparer.Ordinal);
            AssertSpaceMatches(linkSnapshot.AvailableBytes, targetSnapshot.ReadAvailableBytes());
            AssertSpaceMatches(linkSnapshot.ReadAvailableBytes(), targetSnapshot.ReadAvailableBytes());
            AssertSpaceMatches(linkChildSnapshot.AvailableBytes, targetChildSnapshot.ReadAvailableBytes());
            AssertSpaceMatches(linkChildSnapshot.ReadAvailableBytes(), targetChildSnapshot.ReadAvailableBytes());
        }
        finally
        {
            if (Directory.Exists(linkDirectory)) Directory.Delete(linkDirectory, recursive: false);
            if (Directory.Exists(targetChildDirectory)) Directory.Delete(targetChildDirectory, recursive: false);
            if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, recursive: false);
        }
    }

    /// <summary>
    /// 验证可用空间差值处于允许范围内。
    /// </summary>
    /// <param name="actual">实际观测值。</param>
    /// <param name="expected">预期的断言结果。</param>
    private static void AssertSpaceMatches(long actual, long expected)
    {
        var difference = actual >= expected ? actual - expected : expected - actual;
        Assert.True(difference <= DiskSpaceComparisonToleranceBytes,
            $"Expected available space within {DiskSpaceComparisonToleranceBytes} bytes, actual={actual}, expected={expected}.");
    }

    /// <summary>
    /// 验证目录所属 Windows 卷与实际卷一致。
    /// </summary>
    /// <param name="directory">待查询或采样的目录。</param>
    /// <param name="actualVolume">实际解析出的卷路径。</param>
    private static void AssertWindowsVolumeMatches(string directory, string actualVolume)
    {
        Assert.True(string.Equals(
            NormalizeWindowsVolumePath(GetWindowsVolumePath(directory)),
            NormalizeWindowsVolumePath(actualVolume),
            StringComparison.OrdinalIgnoreCase),
            $"Expected actual volume for '{directory}' to match '{actualVolume}'.");
    }

    /// <summary>
    /// 读取目录所属的 Windows 卷路径。
    /// </summary>
    /// <param name="directory">待查询或采样的目录。</param>
    /// <returns>Windows API 返回的卷路径。</returns>
    internal static string GetWindowsVolumePath(string directory)
    {
        var buffer = new StringBuilder(512);
        if (!GetVolumePathNameW(directory, buffer, (uint)buffer.Capacity))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to resolve the volume for '{directory}'.");
        return buffer.ToString();
    }

    /// <summary>
    /// 规范化 Windows 卷路径的结尾分隔符。
    /// </summary>
    /// <param name="path">需要规范化的 Windows 卷路径。</param>
    /// <returns>包含结尾目录分隔符的绝对卷路径。</returns>
    private static string NormalizeWindowsVolumePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// 查询 Windows 目录所在卷的可用字节数。
    /// </summary>
    /// <param name="directory">待查询或采样的目录。</param>
    /// <returns>调用方在目标卷上可用的字节数。</returns>
    private static long QueryWindowsAvailableBytes(string directory)
    {
        if (!GetDiskFreeSpaceExW(directory, out var availableBytes, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to query available space for '{directory}'.");

        return checked((long)availableBytes);
    }

    /// <summary>
    /// 创建指向目标目录的 Windows 目录联接。
    /// </summary>
    /// <param name="junctionDirectory">待创建的 Windows 目录联接路径。</param>
    /// <param name="targetDirectory">目录联接指向的目标目录。</param>
    private static void CreateJunction(string junctionDirectory, string targetDirectory)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("$ErrorActionPreference = 'Stop'; New-Item -ItemType Junction -Path $env:BING_CAPACITY_JUNCTION_PATH -Target $env:BING_CAPACITY_TARGET_PATH | Out-Null");
        startInfo.Environment["BING_CAPACITY_JUNCTION_PATH"] = junctionDirectory;
        startInfo.Environment["BING_CAPACITY_TARGET_PATH"] = targetDirectory;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start PowerShell to create a test junction.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"PowerShell failed to create the test junction: {standardError.Result}{standardOutput.Result}");
    }

    /// <summary>
    /// 调用 Windows API 获取文件所属卷的路径。
    /// </summary>
    /// <param name="fileName">待创建的测试数据库文件名。</param>
    /// <param name="volumePathName">接收卷路径的字符缓冲区。</param>
    /// <param name="bufferLength">卷路径缓冲区的字符容量。</param>
    /// <returns>成功写入卷路径时为 true，调用失败时为 false。</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumePathNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(string fileName, StringBuilder volumePathName, uint bufferLength);

    /// <summary>
    /// 调用 Windows API 查询磁盘可用及总空间。
    /// </summary>
    /// <param name="directoryName">传给磁盘空间 API 的目录路径。</param>
    /// <param name="freeBytesAvailable">接收调用方可用字节数的输出值。</param>
    /// <param name="totalNumberOfBytes">接收磁盘总字节数的指针；零表示不读取。</param>
    /// <param name="totalNumberOfFreeBytes">接收磁盘总空闲字节数的指针；零表示不读取。</param>
    /// <returns>成功读取空间信息时为 true，调用失败时为 false。</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDiskFreeSpaceExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailable,
        IntPtr totalNumberOfBytes,
        IntPtr totalNumberOfFreeBytes);
}

/// <summary>
/// 仅在配置跨卷目标目录的 Windows 环境中运行测试。
/// </summary>
public sealed class CrossVolumeJunctionFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="CrossVolumeJunctionFactAttribute" /> 类型的实例。
    /// </summary>
    public CrossVolumeJunctionFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Cross-volume junction verification requires Windows.";
            return;
        }

        var configuredTarget = Environment.GetEnvironmentVariable("BING_CAPACITY_TEST_OTHER_VOLUME_ROOT");
        if (string.IsNullOrWhiteSpace(configuredTarget) || !Directory.Exists(configuredTarget))
        {
            Skip = "Set BING_CAPACITY_TEST_OTHER_VOLUME_ROOT to an existing directory on a volume different from the system temp volume.";
            return;
        }

        var tempVolume = CapacityProbeDiskSpaceTests.GetWindowsVolumePath(Path.GetFullPath(Path.GetTempPath()));
        var targetVolume = CapacityProbeDiskSpaceTests.GetWindowsVolumePath(Path.GetFullPath(configuredTarget));
        if (string.Equals(tempVolume, targetVolume, StringComparison.OrdinalIgnoreCase))
            Skip = "BING_CAPACITY_TEST_OTHER_VOLUME_ROOT must be on a volume different from the system temp volume.";
    }
}

/// <summary>
/// 仅在 Linux 或 macOS 环境中运行符号链接测试。
/// </summary>
public sealed class UnixSymbolicLinkFactAttribute : FactAttribute
{
    /// <summary>
    /// 初始化一个 <see cref="UnixSymbolicLinkFactAttribute" /> 类型的实例。
    /// </summary>
    public UnixSymbolicLinkFactAttribute()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Skip = "Symbolic link volume verification requires Linux or macOS.";
    }
}
