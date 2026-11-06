using System.Text;
using Bing.AspNetCore.Extensions;
using Bing.AspNetCore.Uploads;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.FileStorage.AspNetCore.Tests;

/// <summary>
/// 覆盖输入在计时外创建；CompareSnapshot始终使用同一个未变旧快照等测试场景。
/// </summary>
public sealed class LegacyUploadTests
{
    /// <summary>
    /// 验证旧上传入口按实际写入字节计算 MD5。
    /// </summary>
    /// <param name="text">待写入并校验 MD5 的文本内容。</param>
    /// <param name="expected">预期的断言结果。</param>
    [Theory]
    [InlineData("abc", "900150983CD24FB0D6963F7D28E17F72")]
    [InlineData("", "D41D8CD98F00B204E9800998ECF8427E")]
    public async Task SaveWithMd5_HashesWrittenBytes(string text, string expected)
    {
        var services = new ServiceCollection(); services.AddUploadService();
        using var provider = services.BuildServiceProvider();
        var path = Path.Combine(Path.GetTempPath(), "bing-md5-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var file = new FormFile(input, 0, input.Length, "file", "sample.txt");
            var hash = await provider.GetRequiredService<IFileUploadService>().SaveWithMd5Async(file, path);
            Assert.Equal(expected, hash);
            Assert.Equal(Encoding.UTF8.GetBytes(text), await File.ReadAllBytesAsync(path));
            Assert.True(input.CanRead);
        }
        finally { File.Delete(path); }
    }
}
