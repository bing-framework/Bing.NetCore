using Bing.Caching;
using Shouldly;
using Xunit;

namespace Bing.Caching.Tests;

/// <summary>
/// 缓存配置、键格式和分片拓扑测试。
/// </summary>
public class CacheConfigurationAndKeyFormatTest
{
    /// <summary>
    /// 验证默认清理模式及其可配置切换。
    /// </summary>
    [Fact]
    public void SyncCleanupMode_DefaultsToSequential()
    {
        var configuration = new CacheConfiguration("app", "redis");

        configuration.SyncCleanupMode.ShouldBe(CacheSyncCleanupMode.Sequential);
        configuration.WithInputLimits(new CacheInputLimits(maxLogicalKeyLength: 8))
            .SyncCleanupMode.ShouldBe(CacheSyncCleanupMode.Sequential);
        new CacheConfiguration("app", "redis",
                topology: CacheTopology.ClientSharded, expectedShardCount: 2)
            .WithSyncCleanupMode(CacheSyncCleanupMode.BoundedParallel)
            .SyncCleanupMode.ShouldBe(CacheSyncCleanupMode.BoundedParallel);
    }

    /// <summary>
    /// 验证物理键格式与原 V2 格式逐字节兼容。
    /// </summary>
    [Fact]
    public void PhysicalKey_RemainsByteForByteCompatibleWithOriginalV2Format()
    {
        var configuration = new CacheConfiguration("租户:{one}", "redis:provider");
        using var sha = System.Security.Cryptography.SHA256.Create();
        foreach (var key in new[] { "item", "订单:*?\\%{two}", "123", "😀", "𐀀" })
        {
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(configuration.Namespace + ":" + configuration.ProviderName + ":" + key));
            var reference = "bing:v2:" + Uri.EscapeDataString(configuration.Namespace) + ":" +
                Uri.EscapeDataString(configuration.ProviderName) + ":{" +
                BitConverter.ToString(hash, 0, 16).Replace("-", "").ToLowerInvariant() + "}:" + Uri.EscapeDataString(key);
            CacheKeyFormat.GetPhysicalKey(configuration, key).ShouldBe(reference);
            CacheKeyFormat.GetPhysicalKeys(configuration, new[] { key })[0].ShouldBe(reference);
        }
    }
    /// <summary>
    /// 验证物理键按命名空间和 Provider 隔离，并能还原特殊字符逻辑键。
    /// </summary>
    [Fact]
    public void PhysicalKey_Isolated_AndRoundTripsSpecialCharacters()
    {
        var left = new CacheConfiguration("tenant:a", "redis");
        var right = new CacheConfiguration("tenant:b", "redis");
        const string logical = "订单:{a}*?\\中文";
        var physical = CacheKeyFormat.GetPhysicalKey(left, logical);

        physical.ShouldStartWith("bing:v2:");
        physical.ShouldNotBe(CacheKeyFormat.GetPhysicalKey(right, logical));
        physical.ShouldNotBe(CacheKeyFormat.GetPhysicalKey(new CacheConfiguration("tenant:a", "memory"), logical));
        CacheKeyFormat.TryGetLogicalKey(left, physical, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(logical);
        CacheKeyFormat.TryGetLogicalKey(right, physical, out _).ShouldBeFalse();
        CacheKeyFormat.TryGetLogicalKey(left, CacheKeyFormat.GetLeaseKey(left, logical), out _).ShouldBeFalse();
        CacheKeyFormat.GetLeaseKey(left, logical).ShouldContain(physical + ":lease");
        CacheKeyFormat.TryGetLogicalKey(left, CacheKeyFormat.GetPhysicalKey(left, "lease"), out var leaseNamedKey)
            .ShouldBeTrue();
        leaseNamedKey.ShouldBe("lease");
    }

    /// <summary>
    /// 验证物理键解码要求规范哈希标签和转义格式。
    /// </summary>
    [Fact]
    public void TryGetLogicalKey_RequiresCanonicalHashTagAndEscaping()
    {
        var configuration = new CacheConfiguration("tenant:a", "redis");
        const string logical = "path/订单~:{a}%?\\中文";
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, logical);

        CacheKeyFormat.TryGetLogicalKey(configuration, physical, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(logical);

        var hashStart = physical.IndexOf('{');
        hashStart.ShouldBeGreaterThanOrEqualTo(0);
        var originalHashCharacter = physical[hashStart + 1];
        var replacementHashCharacter = originalHashCharacter == '0' ? '1' : '0';
        var tamperedHash = physical.Substring(0, hashStart + 1) + replacementHashCharacter +
                           physical.Substring(hashStart + 2);
        CacheKeyFormat.TryGetLogicalKey(configuration, tamperedHash, out _).ShouldBeFalse();

        var lowercasePercent = physical.Replace("%2F", "%2f", StringComparison.Ordinal);
        CacheKeyFormat.TryGetLogicalKey(configuration, lowercasePercent, out _).ShouldBeFalse();

        var percentEncodedUnescapedCharacter = physical.Replace("~", "%7E", StringComparison.Ordinal);
        CacheKeyFormat.TryGetLogicalKey(configuration, percentEncodedUnescapedCharacter, out _)
            .ShouldBeFalse();
    }

    /// <summary>
    /// 验证规范 ASCII 逻辑键往返解码不改变物理格式。
    /// </summary>
    [Fact]
    public void TryGetLogicalKey_RoundTripsCanonicalAsciiWithoutChangingPhysicalFormat()
    {
        var configuration = new CacheConfiguration("tenant:a", "redis");
        foreach (var logical in new[] { "item-1", "safe._~", "plain" })
        {
            var physical = CacheKeyFormat.GetPhysicalKey(configuration, logical);

            CacheKeyFormat.TryGetLogicalKey(configuration, physical, out var decoded).ShouldBeTrue();
            decoded.ShouldBe(logical);
            physical.ShouldBe(CacheKeyFormat.GetPhysicalKey(configuration, decoded));
        }
    }

    /// <summary>
    /// 验证逻辑键解码拒绝相邻命名空间和租约键。
    /// </summary>
    [Fact]
    public void TryGetLogicalKey_RejectsSimilarNamespaceAndLeaseKeys()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var similarNamespace = new CacheConfiguration("tenant-similar", "redis");
        const string logical = "special:{x}*?\\中文%2F";
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, logical);

        CacheKeyFormat.TryGetLogicalKey(similarNamespace, physical, out _).ShouldBeFalse();
        CacheKeyFormat.TryGetLogicalKey(configuration, CacheKeyFormat.GetLeaseKey(configuration, logical), out _)
            .ShouldBeFalse();

        CacheKeyFormat.TryGetLogicalKey(configuration, CacheKeyFormat.GetPhysicalKey(configuration, "special"),
                out var ordinaryKey)
            .ShouldBeTrue();
        ordinaryKey.ShouldBe("special");
    }

    /// <summary>
    /// 验证规范 ASCII 前缀匹配使用快速路径并校验哈希。
    /// </summary>
    [Fact]
    public void TryMatchLogicalPrefix_UsesCanonicalAsciiFastPathAndValidatesHash()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, "item:001");
        using var hashContext = CacheKeyFormat.CreateHashContext(configuration);

        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "item").ShouldBeTrue();
        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "item:001").ShouldBeTrue();
        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "other").ShouldBeFalse();

        var hashStart = physical.IndexOf('{');
        var replacement = physical[hashStart + 1] == '0' ? '1' : '0';
        var tampered = physical.Substring(0, hashStart + 1) + replacement + physical.Substring(hashStart + 2);
        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, tampered, "item").ShouldBeFalse();
    }

    /// <summary>
    /// 验证转义和 Unicode 键使用前缀匹配回退路径。
    /// </summary>
    [Fact]
    public void TryMatchLogicalPrefix_FallsBackForEscapedAndUnicodeKeys()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var logical = "订单:{a}*?\\中文%2F";
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, logical);
        using var hashContext = CacheKeyFormat.CreateHashContext(configuration);

        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "订单:").ShouldBeTrue();
        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "订单:{a}").ShouldBeTrue();
        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical, "订单:x").ShouldBeFalse();
    }

    /// <summary>
    /// 验证前缀匹配拒绝相邻命名空间和租约键。
    /// </summary>
    [Fact]
    public void TryMatchLogicalPrefix_RejectsSimilarNamespaceAndLeaseKeys()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var similar = new CacheConfiguration("tenant-similar", "redis");
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, "item");
        using var hashContext = CacheKeyFormat.CreateHashContext(configuration);
        using var similarHashContext = CacheKeyFormat.CreateHashContext(similar);

        CacheKeyFormat.TryMatchLogicalPrefix(hashContext, physical + ":lease", "item").ShouldBeFalse();
        CacheKeyFormat.TryMatchLogicalPrefix(similarHashContext, physical, "item").ShouldBeFalse();
    }

    /// <summary>
    /// 验证带编码前缀的逻辑键解码保留规范校验。
    /// </summary>
    [Fact]
    public void TryGetLogicalKeyWithPrefix_UsesEncodedPrefixAndPreservesCanonicalValidation()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var similar = new CacheConfiguration("tenant-similar", "redis");
        var logical = "订单:{a}*?\\中文%2F";
        var physical = CacheKeyFormat.GetPhysicalKey(configuration, logical);
        using var hashContext = CacheKeyFormat.CreateHashContext(configuration);
        using var similarHashContext = CacheKeyFormat.CreateHashContext(similar);

        CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physical,
            CacheKeyFormat.GetEncodedLogicalPrefix("订单:"), out var decoded).ShouldBeTrue();
        decoded.ShouldBe(logical);
        CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, physical,
            CacheKeyFormat.GetEncodedLogicalPrefix("other"), out _).ShouldBeFalse();
        CacheKeyFormat.TryGetLogicalKeyWithPrefix(similarHashContext, physical,
            CacheKeyFormat.GetEncodedLogicalPrefix("订单:"), out _).ShouldBeFalse();

        var hashStart = physical.IndexOf('{');
        var replacement = physical[hashStart + 1] == '0' ? '1' : '0';
        var tampered = physical.Substring(0, hashStart + 1) + replacement + physical.Substring(hashStart + 2);
        CacheKeyFormat.TryGetLogicalKeyWithPrefix(hashContext, tampered,
            CacheKeyFormat.GetEncodedLogicalPrefix("订单:"), out _).ShouldBeFalse();
    }

    /// <summary>
    /// 验证前缀模式使用字面量编码前缀并保留哈希校验提示。
    /// </summary>
    [Fact]
    public void PrefixPattern_UsesLiteralEncodedPrefixAndKeepsHashValidationHint()
    {
        var configuration = new CacheConfiguration("tenant", "redis");

        var pattern = CacheKeyFormat.GetPrefixPattern(configuration, "item:*?[]\\订单");

        pattern.ShouldBe("bing:v2:tenant:redis:{" + new string('?', 32) + "}:item%3A%2A%3F%5B%5D%5C%E8%AE%A2%E5%8D%95*");
        pattern.ShouldNotContain("**");
        pattern.ShouldNotContain("[item");
    }

    /// <summary>
    /// 验证前缀和模式接受配对代理字符。
    /// </summary>
    [Fact]
    public void PrefixAndPattern_AllowWellFormedSurrogatePair()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        var prefix = "订单-\U0001F600:";
        var pattern = prefix + "*";

        Should.NotThrow(() => CacheInputValidator.ValidatePrefix(configuration, prefix));
        Should.NotThrow(() => CacheInputValidator.ValidatePattern(configuration, pattern));
        CacheKeyFormat.GetPrefixPattern(configuration, prefix)
            .ShouldContain(Uri.EscapeDataString(prefix));
        CachePattern.CreateMatcher(pattern).LiteralPrefix.ShouldBe(prefix);
    }

    /// <summary>
    /// 验证前缀和模式在编码前拒绝未配对代理字符。
    /// </summary>
    [Fact]
    public void PrefixAndPattern_RejectUnpairedSurrogatesBeforeEncoding()
    {
        var configuration = new CacheConfiguration("tenant", "redis");
        foreach (var invalid in new[] { "\uD800", "\uDC00" })
        {
            var prefixException = Should.Throw<ArgumentException>(() =>
                CacheInputValidator.ValidatePrefix(configuration, "item:" + invalid));
            prefixException.ParamName.ShouldBe("prefix");
            prefixException.Message.ShouldContain("未配对");

            var patternException = Should.Throw<ArgumentException>(() =>
                CacheInputValidator.ValidatePattern(configuration, "item:" + invalid + "*"));
            patternException.ParamName.ShouldBe("pattern");
            patternException.Message.ShouldContain("未配对");
        }
    }

    /// <summary>
    /// 验证配置接受配对代理字符且物理键保持隔离。
    /// </summary>
    [Fact]
    public void Configuration_AllowsWellFormedSurrogatePairs_AndKeepsPhysicalKeyIsolation()
    {
        const string surrogatePair = "\U0001F600";
        var pairConfiguration = new CacheConfiguration("tenant-" + surrogatePair,
            "redis-" + surrogatePair);
        var replacementConfiguration = new CacheConfiguration("tenant-\uFFFD", "redis-\uFFFD");

        pairConfiguration.Namespace.ShouldBe("tenant-" + surrogatePair);
        pairConfiguration.ProviderName.ShouldBe("redis-" + surrogatePair);
        CacheKeyFormat.GetPhysicalKey(pairConfiguration, "item")
            .ShouldNotBe(CacheKeyFormat.GetPhysicalKey(replacementConfiguration, "item"));
    }

    /// <summary>
    /// 验证配置拒绝命名空间和 Provider 名称中的未配对代理字符。
    /// </summary>
    [Fact]
    public void Configuration_RejectsUnpairedNamespaceAndProviderSurrogates()
    {
        foreach (var invalid in new[] { "\uD800", "\uDC00" })
        {
            var namespaceException = Should.Throw<ArgumentException>(() =>
                new CacheConfiguration("tenant-" + invalid, "redis"));
            namespaceException.GetType().ShouldBe(typeof(ArgumentException));
            namespaceException.ParamName.ShouldBe("nameSpace");
            namespaceException.Message.ShouldContain("缓存命名空间");
            namespaceException.Message.ShouldContain("未配对");

            var providerException = Should.Throw<ArgumentException>(() =>
                new CacheConfiguration("tenant", "redis-" + invalid));
            providerException.GetType().ShouldBe(typeof(ArgumentException));
            providerException.ParamName.ShouldBe("providerName");
            providerException.Message.ShouldContain("缓存提供器名称");
            providerException.Message.ShouldContain("未配对");
        }
    }

    /// <summary>
    /// 验证批量物理键格式与逐键生成一致，并保留空输入行为。
    /// </summary>
    [Fact]
    public void PhysicalKeys_BatchMatchesSingleKeyFormat()
    {
        var configuration = new CacheConfiguration("tenant:a", "redis");
        var keys = new[] { "item:1", "item:1", "订单:{a}*?\\中文" };

        var physicalKeys = CacheKeyFormat.GetPhysicalKeys(configuration, keys);

        physicalKeys.ShouldBe(keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray());
        CacheKeyFormat.GetPhysicalKeys(configuration, Array.Empty<string>()).ShouldBeEmpty();
        Should.Throw<ArgumentException>(() => CacheKeyFormat.GetPhysicalKeys(configuration, new[] { "valid", " " }));
    }

    /// <summary>
    /// 验证批量物理键快照不受后续生成影响。
    /// </summary>
    [Fact]
    public void PhysicalKeys_BatchResultsRemainStableAfterLaterGeneration()
    {
        var configuration = new CacheConfiguration("tenant:a", "redis");
        var firstKeys = new[] { "first", "second", "第三个" };
        var firstPhysicalKeys = CacheKeyFormat.GetPhysicalKeys(configuration, firstKeys);
        var snapshot = firstPhysicalKeys.ToArray();

        CacheKeyFormat.GetPhysicalKeys(configuration,
            Enumerable.Range(0, 64).Select(index => "later:" + index).ToArray());

        firstPhysicalKeys.ShouldBe(snapshot);
        firstPhysicalKeys.ShouldBe(firstKeys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray());
    }

    /// <summary>
    /// 验证缓存配置参数校验及过期时间毫秒取整。
    /// </summary>
    [Fact]
    public void Configuration_ValidatesAndRoundsExpiration()
    {
        Should.Throw<ArgumentException>(() => new CacheConfiguration(" ", "redis"));
        Should.Throw<ArgumentOutOfRangeException>(() => new CacheConfiguration("app", "redis", batchSize: 0));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new CacheConfiguration("app", "redis", topology: CacheTopology.ClientSharded));
        new CacheConfiguration("app", "redis", topology: CacheTopology.ClientSharded, expectedShardCount: 2)
            .ExpectedShardCount.ShouldBe(2);
        var configuration = new CacheConfiguration("app", "redis");
        configuration.DefaultExpiration.ShouldBe(TimeSpan.FromHours(8));
        configuration.GetExpiration(new CacheOptions { Expiration = TimeSpan.FromTicks(1) })
            .ShouldBe(TimeSpan.FromMilliseconds(1));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            configuration.GetExpiration(new CacheOptions { Expiration = TimeSpan.Zero }));
    }

    /// <summary>
    /// 验证输入限制默认无限制且应用后保留配置身份。
    /// </summary>
    [Fact]
    public void InputLimits_DefaultToUnlimited_AndCanBeAppliedWithoutChangingConfigurationIdentity()
    {
        var configuration = new CacheConfiguration("app", "redis", batchSize: 7, maxConcurrency: 3);

        configuration.InputLimits.ShouldBe(CacheInputLimits.Unlimited);
        var limits = new CacheInputLimits(maxLogicalKeyLength: 10, maxPrefixLength: 4,
            maxPatternLength: 6);
        var limited = configuration.WithInputLimits(limits);

        limited.InputLimits.ShouldBe(limits);
        limited.Namespace.ShouldBe(configuration.Namespace);
        limited.ProviderName.ShouldBe(configuration.ProviderName);
        limited.DefaultExpiration.ShouldBe(configuration.DefaultExpiration);
        limited.BatchSize.ShouldBe(configuration.BatchSize);
        limited.MaxConcurrency.ShouldBe(configuration.MaxConcurrency);
        limited.PhysicalKeyPrefix.ShouldBe(configuration.PhysicalKeyPrefix);
        limited.HashInputPrefix.ShouldBe(configuration.HashInputPrefix);
    }

    /// <summary>
    /// 验证输入限制拒绝负值和 null 替换配置。
    /// </summary>
    [Fact]
    public void InputLimits_RejectNegativeValuesAndNullReplacement()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new CacheInputLimits(maxLogicalKeyLength: -1))
            .ParamName.ShouldBe("maxLogicalKeyLength");
        Should.Throw<ArgumentOutOfRangeException>(() => new CacheInputLimits(maxPrefixLength: -1))
            .ParamName.ShouldBe("maxPrefixLength");
        Should.Throw<ArgumentOutOfRangeException>(() => new CacheInputLimits(maxPatternLength: -1))
            .ParamName.ShouldBe("maxPatternLength");

        var configuration = new CacheConfiguration("app", "redis");
        Should.Throw<ArgumentNullException>(() => configuration.WithInputLimits(null))
            .ParamName.ShouldBe("inputLimits");
    }

    /// <summary>
    /// 验证输入限制按 UTF-16 代码单元计数并允许边界值。
    /// </summary>
    [Fact]
    public void InputLimits_CountUtf16CodeUnits_AndAllowTheExactBoundary()
    {
        var configuration = new CacheConfiguration("app", "redis")
            .WithInputLimits(new CacheInputLimits(maxLogicalKeyLength: 2));
        var surrogatePair = "\U0001F600";

        surrogatePair.Length.ShouldBe(2);
        Should.NotThrow(() => CacheKeyFormat.GetPhysicalKey(configuration, surrogatePair));

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            CacheKeyFormat.GetPhysicalKey(configuration, surrogatePair + "x"));
        exception.ParamName.ShouldBe("key");
    }

    /// <summary>
    /// 验证物理键生成接受配对代理字符并可正确解码。
    /// </summary>
    [Fact]
    public void PhysicalKey_AllowsWellFormedSurrogatePair()
    {
        var configuration = new CacheConfiguration("app", "redis");
        var logicalKey = "订单-\U0001F600";

        var physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey);

        physicalKey.ShouldNotBeNullOrEmpty();
        CacheKeyFormat.TryGetLogicalKey(configuration, physicalKey, out var decoded).ShouldBeTrue();
        decoded.ShouldBe(logicalKey);
    }

    /// <summary>
    /// 验证单键物理键生成拒绝未配对代理字符且不返回结果。
    /// </summary>
    [Fact]
    public void PhysicalKey_RejectsUnpairedSurrogateWithoutReturningPhysicalKey()
    {
        var configuration = new CacheConfiguration("app", "redis");
        foreach (var logicalKey in new[] { "\uD800", "\uDC00" })
        {
            string physicalKey = null;

            var exception = Should.Throw<ArgumentException>(() =>
                physicalKey = CacheKeyFormat.GetPhysicalKey(configuration, logicalKey));

            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("key");
            exception.Message.ShouldContain("未配对");
            physicalKey.ShouldBeNull();
        }
    }

    /// <summary>
    /// 验证批量物理键生成拒绝未配对代理字符且不返回结果。
    /// </summary>
    [Fact]
    public void PhysicalKeys_RejectsUnpairedSurrogateWithoutReturningPhysicalKeys()
    {
        var configuration = new CacheConfiguration("app", "redis");
        foreach (var invalidKey in new[] { "\uD800", "\uDC00" })
        {
            string[] physicalKeys = null;

            var exception = Should.Throw<ArgumentException>(() =>
                physicalKeys = CacheKeyFormat.GetPhysicalKeys(configuration, new[] { "valid", invalidKey }));

            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("key");
            exception.Message.ShouldContain("未配对");
            physicalKeys.ShouldBeNull();
        }
    }

    /// <summary>
    /// 验证租约键格式化前拒绝未配对代理字符。
    /// </summary>
    [Fact]
    public void LeaseKey_RejectsUnpairedSurrogateBeforeFormatting()
    {
        var configuration = new CacheConfiguration("app", "redis");
        foreach (var invalidKey in new[] { "\uD800", "\uDC00" })
        {
            var exception = Should.Throw<ArgumentException>(() =>
                CacheKeyFormat.GetLeaseKey(configuration, invalidKey));

            exception.ParamName.ShouldBe("key");
            exception.Message.ShouldContain("未配对");
        }
    }

    /// <summary>
    /// 验证输入限制同时作用于批量键和租约键。
    /// </summary>
    [Fact]
    public void InputLimits_ApplyToBatchKeysAndLeaseKeys()
    {
        var configuration = new CacheConfiguration("app", "redis")
            .WithInputLimits(new CacheInputLimits(maxLogicalKeyLength: 4));
        var keys = new[] { "same", "same" };

        CacheKeyFormat.GetPhysicalKeys(configuration, keys)
            .ShouldBe(keys.Select(key => CacheKeyFormat.GetPhysicalKey(configuration, key)).ToArray());
        Should.Throw<ArgumentOutOfRangeException>(() =>
            CacheKeyFormat.GetPhysicalKeys(configuration, new[] { "ok", "too-long" }))
            .ParamName.ShouldBe("key");
        Should.Throw<ArgumentOutOfRangeException>(() =>
            CacheKeyFormat.GetLeaseKey(configuration, "too-long"))
            .ParamName.ShouldBe("key");
    }

    /// <summary>
    /// 验证默认输入限制兼容超长逻辑键。
    /// </summary>
    [Fact]
    public void InputLimits_DefaultCompatibilityAllowsLongLogicalKeys()
    {
        var configuration = new CacheConfiguration("app", "redis");
        var logicalKey = new string('k', 4096);

        Should.NotThrow(() => CacheKeyFormat.GetPhysicalKey(configuration, logicalKey));
        Should.NotThrow(() => CacheKeyFormat.GetPhysicalKeys(configuration, new[] { logicalKey }));
    }

    /// <summary>
    /// 验证分片拓扑拒绝重复服务器身份。
    /// </summary>
    [Fact]
    public void ShardTopology_RejectsRepeatedServerIdentity()
    {
        const string one = "# Server\r\nrun_id:first\r\n";
        const string two = "# Server\r\nrun_id:second\r\n";
        Should.NotThrow(() => CacheShardTopology.Validate(new[] { one, two }, 2));
        Should.Throw<InvalidOperationException>(() => CacheShardTopology.Validate(new[] { one, one }, 2));
    }
}
