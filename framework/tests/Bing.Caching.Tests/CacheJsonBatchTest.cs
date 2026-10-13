using System.Text;
using Bing.Caching;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// CacheJsonBatch JSON 批量构造测试。
/// </summary>
public class CacheJsonBatchTest
{
    /// <summary>
    /// 验证字符串 JSON 片段保留，并为缺失值使用默认令牌。
    /// </summary>
    [Fact]
    public void Build_StringValues_ShouldPreserveJsonFragmentsAndDefaultToken()
    {
        var result = CacheJsonBatch.Build(
            new[] { "1", null, "   ", " null ", "{\"name\":\"value\"}", "\"text\"" },
            "0");

        result.ShouldBe("[1,0,0,0,{\"name\":\"value\"},\"text\"]");
        JsonConvert.DeserializeObject<int[]>(CacheJsonBatch.Build(new[] { "1", null, "3" }, "0"))
            .ShouldBe(new[] { 1, 0, 3 });
    }

    /// <summary>
    /// 验证 null 默认令牌只替换缺失或空白输入。
    /// </summary>
    [Fact]
    public void Build_StringValues_WithNullDefault_ShouldReplaceOnlyMissingOrWhitespace()
    {
        CacheJsonBatch.Build(new[] { null, "\t", " null ", "2" }, null)
            .ShouldBe("[null,null, null ,2]");
    }

    /// <summary>
    /// 验证字节值按 UTF-8 解码，并为缺失值使用 JSON null。
    /// </summary>
    [Fact]
    public void Build_ByteValues_ShouldDecodeUtf8AndUseJsonNullForMissing()
    {
        var result = CacheJsonBatch.Build(
            new byte[][]
            {
                Encoding.UTF8.GetBytes("\"中文\U0001F600\""),
                null,
                Encoding.UTF8.GetBytes("null")
            },
            null);

        result.ShouldBe("[\"中文\U0001F600\",null,null]");
        JsonConvert.DeserializeObject<string[]>(result)
            .ShouldBe(new[] { "中文\U0001F600", null, null });
    }

    /// <summary>
    /// 验证字节值的空白和 JSON null 使用默认令牌替换。
    /// </summary>
    [Fact]
    public void Build_ByteValues_WithDefaultToken_ShouldReplaceWhitespaceAndJsonNull()
    {
        var result = CacheJsonBatch.Build(
            new byte[][]
            {
                Encoding.UTF8.GetBytes("1"),
                null,
                Encoding.UTF8.GetBytes(" \t"),
                Encoding.UTF8.GetBytes(" null "),
                Encoding.UTF8.GetBytes("null")
            },
            "0");

        result.ShouldBe("[1,0,0,0,0]");
        JsonConvert.DeserializeObject<int[]>(result)
            .ShouldBe(new[] { 1, 0, 0, 0, 0 });
    }

    /// <summary>
    /// 验证 null 默认令牌保留现有 JSON 片段。
    /// </summary>
    [Fact]
    public void Build_ByteValues_WithNullDefault_ShouldPreserveExistingJsonFragments()
    {
        var result = CacheJsonBatch.Build(
            new byte[][]
            {
                null,
                Array.Empty<byte>(),
                Encoding.UTF8.GetBytes(" null "),
                Encoding.UTF8.GetBytes("1")
            },
            null);

        result.ShouldBe("[null,, null ,1]");
    }

    /// <summary>
    /// 验证空输入生成空 JSON 数组。
    /// </summary>
    [Fact]
    public void Build_EmptyValues_ShouldReturnEmptyJsonArray()
    {
        CacheJsonBatch.Build(Array.Empty<string>(), "0").ShouldBe("[]");
        CacheJsonBatch.Build(Array.Empty<byte[]>(), null).ShouldBe("[]");
    }

    /// <summary>
    /// 验证 null 输入被拒绝。
    /// </summary>
    [Fact]
    public void Build_NullInput_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CacheJsonBatch.Build((string[])null, null))
            .ParamName.ShouldBe("rawValues");
        Should.Throw<ArgumentNullException>(() => CacheJsonBatch.Build((byte[][])null, "0"))
            .ParamName.ShouldBe("rawValues");
    }

    /// <summary>
    /// 验证大 Unicode 片段保持边界和可反序列化结果。
    /// </summary>
    [Fact]
    public void Build_ShouldHandleLargeUnicodeFragmentsWithoutChangingBoundaries()
    {
        var value = "\"" + new string('\u4E2D', 2048) + "\"";
        var result = CacheJsonBatch.Build(new[] { value, null, value }, null);

        result.Length.ShouldBe(value.Length * 2 + "null".Length + 4);
        result[0].ShouldBe('[');
        result[result.Length - 1].ShouldBe(']');
        JsonConvert.DeserializeObject<string[]>(result)
            .ShouldBe(new[] { new string('\u4E2D', 2048), null, new string('\u4E2D', 2048) });
    }

    /// <summary>
    /// 验证替换令牌较长时仍使用安全容量生成 JSON。
    /// </summary>
    [Fact]
    public void Build_ShouldUseSafeCapacityWhenReplacementIsLongerThanUtf8Values()
    {
        const string replacement = "{\"fallback\":true}";
        var stringResult = CacheJsonBatch.Build(new[] { "1", null, " \t", "null" }, replacement);
        stringResult.ShouldBe("[1,{\"fallback\":true},{\"fallback\":true},{\"fallback\":true}]");

        var byteResult = CacheJsonBatch.Build(
            new byte[][]
            {
                Encoding.UTF8.GetBytes("1"),
                null,
                Encoding.UTF8.GetBytes(" \t"),
                Encoding.UTF8.GetBytes("null"),
                Encoding.UTF8.GetBytes("\"中文\"")
            },
            replacement);
        byteResult.ShouldBe("[1,{\"fallback\":true},{\"fallback\":true},{\"fallback\":true},\"中文\"]");
    }
}
