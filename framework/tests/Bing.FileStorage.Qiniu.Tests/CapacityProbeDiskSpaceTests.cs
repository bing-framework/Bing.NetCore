using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Bing.FileStorage.CapacityProbe;
using Xunit;

namespace Bing.FileStorage.Qiniu.Tests;

public sealed class CapacityProbeDiskSpaceTests
{
    private const int PartSize = 16 * 1024 * 1024;
    private const long GiB = 1024L * 1024 * 1024;
    private const string OtherVolumeRootEnvironmentVariable = "BING_CAPACITY_TEST_OTHER_VOLUME_ROOT";
    private const long DiskSpaceComparisonToleranceBytes = 64L * 1024 * 1024;

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

    [Fact]
    public void DefaultResolution_FailsForMissingDirectoryInsteadOfFallingBackToPathRoot()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "bing-capacity-missing-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(missingDirectory));

        Assert.ThrowsAny<IOException>(() => CapacityProbeDiskSpace.Measure(
            "local", missingDirectory, Path.GetTempPath(), 1, 1, 1));
    }

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

    private static void AssertSpaceMatches(long actual, long expected)
    {
        var difference = actual >= expected ? actual - expected : expected - actual;
        Assert.True(difference <= DiskSpaceComparisonToleranceBytes,
            $"Expected available space within {DiskSpaceComparisonToleranceBytes} bytes, actual={actual}, expected={expected}.");
    }

    private static void AssertWindowsVolumeMatches(string directory, string actualVolume)
    {
        Assert.True(string.Equals(
            NormalizeWindowsVolumePath(GetWindowsVolumePath(directory)),
            NormalizeWindowsVolumePath(actualVolume),
            StringComparison.OrdinalIgnoreCase),
            $"Expected actual volume for '{directory}' to match '{actualVolume}'.");
    }

    internal static string GetWindowsVolumePath(string directory)
    {
        var buffer = new StringBuilder(512);
        if (!GetVolumePathNameW(directory, buffer, (uint)buffer.Capacity))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to resolve the volume for '{directory}'.");
        return buffer.ToString();
    }

    private static string NormalizeWindowsVolumePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static long QueryWindowsAvailableBytes(string directory)
    {
        if (!GetDiskFreeSpaceExW(directory, out var availableBytes, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Unable to query available space for '{directory}'.");

        return checked((long)availableBytes);
    }

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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetVolumePathNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(string fileName, StringBuilder volumePathName, uint bufferLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDiskFreeSpaceExW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailable,
        IntPtr totalNumberOfBytes,
        IntPtr totalNumberOfFreeBytes);
}

public sealed class CrossVolumeJunctionFactAttribute : FactAttribute
{
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

public sealed class UnixSymbolicLinkFactAttribute : FactAttribute
{
    public UnixSymbolicLinkFactAttribute()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            Skip = "Symbolic link volume verification requires Linux or macOS.";
    }
}
