using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Bing.Test.Shared;

/// <summary>
/// 受保护 Provider 运行的真实环境元数据。
/// </summary>
public sealed class ProviderRunMetadata
{
    /// <summary>
    /// 获取或设置 Provider 版本。
    /// </summary>
    public string ProviderVersion { get; set; }

    /// <summary>
    /// 获取或设置数据库版本。
    /// </summary>
    public string DatabaseVersion { get; set; }

    /// <summary>
    /// 获取或设置驱动版本。
    /// </summary>
    public string DriverVersion { get; set; }

    /// <summary>
    /// 获取或设置运行时版本。
    /// </summary>
    public string RuntimeVersion { get; set; }

    /// <summary>
    /// 获取或设置操作系统名称。
    /// </summary>
    public string OperatingSystem { get; set; }

    /// <summary>
    /// 获取或设置目标框架。
    /// </summary>
    public string TargetFramework { get; set; }
}

/// <summary>
/// 写入受保护 Provider 运行元数据 sidecar。
/// </summary>
public static class ProviderRunMetadataWriter
{
    /// <summary>
    /// Provider 运行元数据输出路径的环境变量名称。
    /// </summary>
    public const string OutputPathEnvironmentVariable = "BING_PROVIDER_RUN_METADATA_PATH";

    /// <summary>
    /// 写入当前测试进程可观测的 Provider、数据库和运行时元数据。
    /// </summary>
    /// <param name="providerVersion">Provider 版本。</param>
    /// <param name="databaseVersion">数据库版本。</param>
    /// <param name="driverVersion">数据库驱动版本。</param>
    /// <param name="testAssembly">用于读取目标框架的测试程序集。</param>
    public static void WriteFromEnvironment(string providerVersion, string databaseVersion, string driverVersion,
        Assembly testAssembly)
    {
        var path = Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (testAssembly == null)
            throw new ArgumentNullException(nameof(testAssembly));
        Write(path, new ProviderRunMetadata
        {
            ProviderVersion = providerVersion,
            DatabaseVersion = databaseVersion,
            DriverVersion = driverVersion,
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            TargetFramework = NormalizeTargetFramework(testAssembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()
                ?.FrameworkName)
        });
    }

    /// <summary>
    /// 将目标框架名称标准化为发布元数据格式。
    /// </summary>
    /// <param name="frameworkName">程序集目标框架名称。</param>
    /// <returns>标准化后的目标框架名称。</returns>
    private static string NormalizeTargetFramework(string frameworkName)
    {
        if (string.IsNullOrWhiteSpace(frameworkName))
            return frameworkName;
        const string marker = "Version=v";
        var index = frameworkName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? frameworkName : "net" + frameworkName[(index + marker.Length)..];
    }

    /// <summary>
    /// 将运行元数据以无 BOM UTF-8 JSON 写入文件。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="metadata">待写入的运行元数据。</param>
    private static void Write(string path, ProviderRunMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        foreach (var value in new[] { metadata.ProviderVersion, metadata.DatabaseVersion, metadata.DriverVersion,
                     metadata.RuntimeVersion, metadata.OperatingSystem, metadata.TargetFramework })
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Provider 运行元数据不完整。", nameof(metadata));
        }
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        if (json.Contains("Password=", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("User Id=", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("Server=", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Provider 运行元数据不能包含连接信息。");
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}