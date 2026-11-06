using Bing.Caching.FusionCache.Internal;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证标签数组快路径与一般枚举保持相同的编码和校验语义。
/// </summary>
public class FusionCacheTagNormalizerTest
{
    /// <summary>
    /// 数组与单次枚举都保持顺序、重复标签和转义，并且不修改调用方数组。
    /// </summary>
    [Fact]
    public void ArrayAndEnumerablePreserveOrderEncodingAndInput()
    {
        var normalizer = new FusionCacheKeyNormalizer(new BingFusionCacheOptions());
        string[] input = ["a:b", "中文", "a:b"];
        var enumerations = 0;
        IEnumerable<string> Enumerate()
        {
            enumerations++;
            foreach (var item in input) yield return item;
        }
        var expected = new[] { "t:a%3Ab", "t:%E4%B8%AD%E6%96%87", "t:a%3Ab" };
        Assert.Equal(expected, normalizer.Tags(input));
        Assert.Equal(expected, normalizer.Tags(Enumerate()));
        Assert.Equal(1, enumerations);
        Assert.Equal(new[] { "a:b", "中文", "a:b" }, input);
        Assert.Null(normalizer.Tags(null));
        Assert.Empty(normalizer.Tags(Array.Empty<string>())!);
    }

    /// <summary>
    /// 数组和枚举在标签无效或超量时以相同顺序报告第一个错误。
    /// </summary>
    [Fact]
    public void ArrayAndEnumerableKeepValidationOrderAndLimits()
    {
        var normalizer = new FusionCacheKeyNormalizer(new BingFusionCacheOptions
        {
            MaxTagsPerEntry = 1,
            MaxTagLength = 3
        });
        string[] invalidFirst = ["", "other"];
        string[] tooMany = ["one", ""];
        Assert.Throws<ArgumentException>(() => normalizer.Tags(invalidFirst));
        Assert.Throws<ArgumentException>(() => normalizer.Tags(invalidFirst.Select(x => x)));
        Assert.Throws<ArgumentOutOfRangeException>(() => normalizer.Tags(tooMany));
        Assert.Throws<ArgumentOutOfRangeException>(() => normalizer.Tags(tooMany.Select(x => x)));
        Assert.Throws<ArgumentOutOfRangeException>(() => normalizer.Tags(new[] { "long" }));
    }
}
