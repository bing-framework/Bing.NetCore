using System.Text;
using System.Text.Json;
using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// Provider 运行元数据 sidecar 写入测试。
/// </summary>
public sealed class ProviderRunMetadataTest
{
    /// <summary>
    /// 测试目的：写入完整运行元数据时应使用 UTF-8 无 BOM，并将目标框架标准化为 net 形式。
    /// </summary>
    [Fact]
    public void WriteFromEnvironment_WhenMetadataIsComplete_ShouldWriteUtf8AndNormalizedTargetFramework()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"provider-metadata-{Guid.NewGuid():N}.json");
        var previousPath = Environment.GetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, path);

            // Act
            ProviderRunMetadataWriter.WriteFromEnvironment("1.2.3", "8.0", "2.1.2",
                typeof(ProviderRunMetadataTest).Assembly);

            // Assert
            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            Assert.Equal("1.2.3", document.RootElement.GetProperty("ProviderVersion").GetString());
            var targetFramework = document.RootElement.GetProperty("TargetFramework").GetString();
            Assert.Matches("^net[0-9]+\\.[0-9]+$", targetFramework);
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("RuntimeVersion").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("OperatingSystem").GetString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, previousPath);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 测试目的：数据库版本缺失时应拒绝写入，避免生成不可发布的占位元数据。
    /// </summary>
    [Fact]
    public void WriteFromEnvironment_WhenDatabaseVersionIsMissing_ShouldRejectIncompleteMetadata()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"provider-metadata-{Guid.NewGuid():N}.json");
        var previousPath = Environment.GetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, path);

            // Act and Assert
            Assert.Throws<ArgumentException>(() => ProviderRunMetadataWriter.WriteFromEnvironment(
                "1.2.3", null, "2.1.2", typeof(ProviderRunMetadataTest).Assembly));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, previousPath);
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// 测试目的：元数据中的连接字符串关键字段必须被拒绝，避免秘密进入证据制品。
    /// </summary>
    [Theory]
    [InlineData("Server=localhost;Database=test;")]
    [InlineData("User Id=integration;")]
    [InlineData("Password=secret;")]
    [InlineData("Data Source=localhost;")]
    public void WriteFromEnvironment_WhenProviderVersionContainsConnectionInfo_ShouldRejectSensitiveMetadata(
        string sensitiveValue)
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"provider-metadata-{Guid.NewGuid():N}.json");
        var previousPath = Environment.GetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, path);

            // Act and Assert
            Assert.Throws<InvalidOperationException>(() => ProviderRunMetadataWriter.WriteFromEnvironment(
                sensitiveValue, "8.0", "2.1.2", typeof(ProviderRunMetadataTest).Assembly));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProviderRunMetadataWriter.OutputPathEnvironmentVariable, previousPath);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}