using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Bing.Caching;

/// <summary>
/// 在缓存实例间隔离物理键，并让数据键和租约键落在同一个 Redis 槽。
/// </summary>
public static class CacheKeyFormat
{
    /// <summary>
    /// 小写十六进制字符表。
    /// </summary>
    private const string HexDigits = "0123456789abcdef";
    /// <summary>
    /// 大写百分号编码字符表。
    /// </summary>
    private const string EscapeHexDigits = "0123456789ABCDEF";
    /// <summary>
    /// 匹配 Redis hash tag 的 glob 通配符。
    /// </summary>
    private static readonly string RedisHashTagWildcard = new string('?', 16 * 2);
    /// <summary>
    /// 标记 URI 转义规范中的 ASCII 字符。
    /// </summary>
    private static readonly bool[] CanonicalAscii = CreateCanonicalAsciiTable();
    /// <summary>
    /// 遇到非法 UTF-8 时抛出异常的严格编码器。
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// 创建规范 ASCII 字符查找表。
    /// </summary>
    /// <returns>标记无需 URI 转义的 ASCII 字符的数组。</returns>
    private static bool[] CreateCanonicalAsciiTable()
    {
        var result = new bool[128];
        for (var index = 0; index < result.Length; index++)
        {
            var character = ((char)index).ToString();
            result[index] = string.Equals(Uri.EscapeDataString(character), character,
                StringComparison.Ordinal);
        }
        return result;
    }

    /// <summary>
    /// 在批量键处理中复用哈希计算资源。
    /// </summary>
    /// <remarks>该上下文应由调用方释放，可减少重复创建哈希实例和 UTF-8 缓冲。</remarks>
    internal sealed class HashContext : IDisposable
    {
        /// <summary>
        /// 当前缓存实例的键配置。
        /// </summary>
        private readonly CacheConfiguration _configuration;
        /// <summary>
        /// 哈希输入前缀的 UTF-8 字节。
        /// </summary>
        private readonly byte[] _prefixBytes;
        /// <summary>
        /// 用于计算 SHA-256 的实例。
        /// </summary>
        private readonly SHA256 _sha256;
        /// <summary>
        /// 池化的哈希输入字节缓冲。
        /// </summary>
        private byte[] _inputBuffer;
        /// <summary>
        /// 池化的 UTF-8 校验字符缓冲。
        /// </summary>
        private char[] _validationBuffer;
        /// <summary>
        /// 指示哈希上下文是否已释放；为 <see langword="true"/> 时不能继续使用。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化一个 <see cref="HashContext"/> 类型的实例。
        /// </summary>
        /// <param name="configuration">缓存键配置。</param>
        internal HashContext(CacheConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _prefixBytes = Encoding.UTF8.GetBytes(configuration.HashInputPrefix);
            _sha256 = SHA256.Create();
            _inputBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, _prefixBytes.Length));
            _validationBuffer = ArrayPool<char>.Shared.Rent(1);
        }

        /// <summary>
        /// 获取当前缓存键配置。
        /// </summary>
        internal CacheConfiguration Configuration => _configuration;

        /// <summary>
        /// 计算逻辑键的哈希值。
        /// </summary>
        /// <param name="key">逻辑键。</param>
        /// <returns>逻辑键对应的 SHA-256 哈希值。</returns>
        internal byte[] ComputeHash(string key)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HashContext));
            // 使用最大字节数作为池化缓冲上界，避免 GetByteCount 后又对同一键做第二次 UTF-8 扫描。
            var maxKeyBytes = Encoding.UTF8.GetMaxByteCount(key.Length);
            EnsureCapacity(checked(_prefixBytes.Length + maxKeyBytes));
            if (_prefixBytes.Length > 0)
                Buffer.BlockCopy(_prefixBytes, 0, _inputBuffer, 0, _prefixBytes.Length);
            var writtenBytes = Encoding.UTF8.GetBytes(key, 0, key.Length, _inputBuffer, _prefixBytes.Length);
            return _sha256.ComputeHash(_inputBuffer, 0, _prefixBytes.Length + writtenBytes);
        }

        /// <summary>
        /// 计算物理键编码后缀还原出的逻辑键哈希值。
        /// </summary>
        /// <param name="physicalKey">物理缓存键。</param>
        /// <param name="encodedStart">编码后缀的起始位置。</param>
        /// <returns>哈希值；后缀无效时返回 <see langword="null"/>。</returns>
        internal byte[] ComputeHashEncodedSuffix(string physicalKey, int encodedStart)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HashContext));
            if (physicalKey == null)
                throw new ArgumentNullException(nameof(physicalKey));
            if (encodedStart < 0 || encodedStart > physicalKey.Length)
                throw new ArgumentOutOfRangeException(nameof(encodedStart));

            var encodedLength = physicalKey.Length - encodedStart;
            EnsureCapacity(_prefixBytes.Length + encodedLength);
            if (_prefixBytes.Length > 0)
                Buffer.BlockCopy(_prefixBytes, 0, _inputBuffer, 0, _prefixBytes.Length);

            var keyLength = 0;
            for (var index = encodedStart; index < physicalKey.Length; index++)
            {
                var character = physicalKey[index];
                byte value;
                if (character < CanonicalAscii.Length && CanonicalAscii[character])
                    value = (byte)character;
                else
                {
                    if (character != '%' || index + 2 >= physicalKey.Length)
                        return null;
                    var high = HexValue(physicalKey[++index]);
                    var low = HexValue(physicalKey[++index]);
                    if (high < 0 || low < 0)
                        return null;
                    value = (byte)((high << 4) | low);
                    if (value < CanonicalAscii.Length && CanonicalAscii[value])
                        return null;
                }
                _inputBuffer[_prefixBytes.Length + keyLength++] = value;
            }

            if (keyLength == 0)
                return null;
            var length = _prefixBytes.Length + keyLength;
            int characterCount;
            try
            {
                characterCount = StrictUtf8.GetCharCount(_inputBuffer, _prefixBytes.Length, keyLength);
                EnsureValidationCapacity(characterCount);
                StrictUtf8.GetChars(_inputBuffer, _prefixBytes.Length, keyLength,
                    _validationBuffer, 0);
            }
            catch (DecoderFallbackException)
            {
                return null;
            }
            for (var index = 0; index < characterCount; index++)
                if (!char.IsWhiteSpace(_validationBuffer[index]))
                    return _sha256.ComputeHash(_inputBuffer, 0, length);
            return null;
        }

        /// <summary>
        /// 将逻辑键的 hash tag 写入字符缓冲区。
        /// </summary>
        /// <param name="key">逻辑键。</param>
        /// <param name="buffer">目标字符缓冲区。</param>
        /// <param name="offset">写入起始位置。</param>
        internal void WriteHashTag(string key, char[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            var hash = ComputeHash(key);
            for (var index = 0; index < 16; index++)
            {
                buffer[offset + index * 2] = HexDigits[hash[index] >> 4];
                buffer[offset + index * 2 + 1] = HexDigits[hash[index] & 15];
            }
        }

        /// <summary>
        /// 确保哈希输入缓冲区容量足够。
        /// </summary>
        /// <param name="required">所需容量。</param>
        private void EnsureCapacity(int required)
        {
            if (_inputBuffer.Length >= required)
                return;
            var replacement = ArrayPool<byte>.Shared.Rent(required);
            ArrayPool<byte>.Shared.Return(_inputBuffer);
            _inputBuffer = replacement;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            ArrayPool<byte>.Shared.Return(_inputBuffer);
            ArrayPool<char>.Shared.Return(_validationBuffer);
            _inputBuffer = null;
            _validationBuffer = null;
            _sha256.Dispose();
        }

        /// <summary>
        /// 确保 UTF-8 校验字符缓冲区容量足够。
        /// </summary>
        /// <param name="required">所需容量。</param>
        private void EnsureValidationCapacity(int required)
        {
            if (_validationBuffer.Length >= required)
                return;
            var replacement = ArrayPool<char>.Shared.Rent(required);
            ArrayPool<char>.Shared.Return(_validationBuffer);
            _validationBuffer = replacement;
        }
    }

    /// <summary>
    /// 创建缓存键哈希上下文。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <returns>缓存键哈希上下文。</returns>
    internal static HashContext CreateHashContext(CacheConfiguration configuration) => new HashContext(configuration);

    /// <summary>
    /// 将逻辑键转换为版本化物理键。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="key">逻辑键。</param>
    /// <returns>隔离后的物理缓存键。</returns>
    public static string GetPhysicalKey(CacheConfiguration configuration, string key)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        CacheInputValidator.ValidateKey(configuration, key);
        var prefix = GetNamespacePrefix(configuration);
        using var hashContext = new HashContext(configuration);
        return FormatPhysicalKey(configuration, key, prefix, hashContext);
    }

    /// <summary>
    /// 将一组逻辑键转换为物理键。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="keys">逻辑键集合。</param>
    /// <returns>按输入顺序排列的物理键。</returns>
    public static string[] GetPhysicalKeys(CacheConfiguration configuration, IList<string> keys)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));
        var result = new string[keys.Count];
        if (result.Length == 0)
            return result;
        var prefix = GetNamespacePrefix(configuration);
        using var hashContext = new HashContext(configuration);
        for (var index = 0; index < result.Length; index++)
        {
            CacheInputValidator.ValidateKey(configuration, keys[index]);
            result[index] = FormatPhysicalKey(configuration, keys[index], prefix, hashContext);
        }
        return result;
    }

    /// <summary>
    /// 格式化逻辑键对应的物理键。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="key">逻辑键。</param>
    /// <param name="prefix">物理命名空间前缀。</param>
    /// <param name="hashContext">复用的哈希上下文。</param>
    /// <returns>格式化后的物理缓存键。</returns>
    private static string FormatPhysicalKey(CacheConfiguration configuration, string key, string prefix,
        HashContext hashContext)
    {
        // GetBytes 同时完成实际编码；容量只需使用安全上界，避免先 GetByteCount 再重复遍历键。
        var maxKeyByteCount = Encoding.UTF8.GetMaxByteCount(key.Length);
        var fixedLength = checked(prefix.Length + 1 + 16 * 2 + 2);
        var capacity = checked(fixedLength + maxKeyByteCount * 3);
        var chars = ArrayPool<char>.Shared.Rent(capacity);
        var bytes = ArrayPool<byte>.Shared.Rent(Math.Max(1, maxKeyByteCount));
        try
        {
            var offset = 0;
            prefix.CopyTo(0, chars, offset, prefix.Length);
            offset += prefix.Length;
            chars[offset++] = '{';
            hashContext.WriteHashTag(key, chars, offset);
            offset += 16 * 2;
            chars[offset++] = '}';
            chars[offset++] = ':';

            var writtenBytes = Encoding.UTF8.GetBytes(key, 0, key.Length, bytes, 0);
            for (var index = 0; index < writtenBytes; index++)
            {
                var value = bytes[index];
                if (value < CanonicalAscii.Length && CanonicalAscii[value])
                {
                    chars[offset++] = (char)value;
                    continue;
                }
                chars[offset++] = '%';
                chars[offset++] = EscapeHexDigits[value >> 4];
                chars[offset++] = EscapeHexDigits[value & 15];
            }
            return new string(chars, 0, offset);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    /// <summary>
    /// 获取数据键对应的租约键。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="key">逻辑数据键。</param>
    /// <returns>同槽位的物理租约键。</returns>
    public static string GetLeaseKey(CacheConfiguration configuration, string key) =>
        GetPhysicalKey(configuration, key) + ":lease";

    /// <summary>
    /// 获取限定当前命名空间的 Redis 扫描模式。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <returns>当前命名空间的物理键扫描模式。</returns>
    public static string GetNamespacePattern(CacheConfiguration configuration) =>
        EscapeRedisGlob(GetNamespacePrefix(configuration)) + "*";

    /// <summary>
    /// 对逻辑前缀执行 URI 转义。
    /// </summary>
    /// <param name="logicalPrefix">逻辑键前缀。</param>
    /// <returns>转义后的前缀；输入为 <see langword="null"/> 时返回 <see langword="null"/>。</returns>
    internal static string GetEncodedLogicalPrefix(string logicalPrefix) =>
        logicalPrefix == null ? null : Uri.EscapeDataString(logicalPrefix);

    /// <summary>
    /// 根据已解析的模式生成扫描模式。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="matcher">已解析的逻辑键模式。</param>
    /// <returns>限定当前命名空间的 Redis 扫描模式。</returns>
    /// <remarks>只下推首个通配符前的字面前缀，完整模式仍需在逻辑键上复核。</remarks>
    internal static string GetPatternScanPattern(CacheConfiguration configuration, CachePattern.Matcher matcher)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (matcher == null)
            throw new ArgumentNullException(nameof(matcher));
        return string.IsNullOrEmpty(matcher.LiteralPrefix)
            ? GetNamespacePattern(configuration)
            : GetPrefixPattern(configuration, matcher.LiteralPrefix);
    }

    /// <summary>
    /// 生成带有逻辑前缀提示的 Redis SCAN 模式。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="logicalPrefix">逻辑键前缀。</param>
    /// <returns>限定当前命名空间和逻辑前缀的扫描模式。</returns>
    /// <remarks>扫描结果仍需通过物理键布局、命名空间和哈希校验。</remarks>
    internal static string GetPrefixPattern(CacheConfiguration configuration, string logicalPrefix)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (string.IsNullOrEmpty(logicalPrefix))
            return GetNamespacePattern(configuration);

        var encodedPrefix = EscapeRedisGlob(GetEncodedLogicalPrefix(logicalPrefix));
        return EscapeRedisGlob(GetNamespacePrefix(configuration)) + "{" + RedisHashTagWildcard + "}:" +
               encodedPrefix + "*";
    }

    /// <summary>
    /// 尝试验证并还原物理缓存键。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <param name="physicalKey">待验证的物理缓存键。</param>
    /// <param name="key">输出的逻辑键。</param>
    /// <returns>验证成功时返回 <see langword="true"/>，并设置逻辑键；否则返回 <see langword="false"/>。</returns>
    public static bool TryGetLogicalKey(CacheConfiguration configuration, string physicalKey, out string key)
    {
        if (configuration == null || physicalKey == null)
        {
            key = null;
            return false;
        }
        using var hashContext = new HashContext(configuration);
        return TryGetLogicalKey(hashContext, physicalKey, out key);
    }

    /// <summary>
    /// 尝试验证并还原物理缓存键。
    /// </summary>
    /// <param name="hashContext">调用方持有的哈希上下文。</param>
    /// <param name="physicalKey">待验证的物理缓存键。</param>
    /// <param name="key">输出的逻辑键。</param>
    /// <returns>验证成功时返回 <see langword="true"/>，并设置逻辑键；否则返回 <see langword="false"/>。</returns>
    internal static bool TryGetLogicalKey(HashContext hashContext, string physicalKey, out string key)
    {
        return TryGetLogicalKeyCore(hashContext, physicalKey, null, out key);
    }

    /// <summary>
    /// 尝试按逻辑前缀验证并还原物理缓存键。
    /// </summary>
    /// <param name="hashContext">调用方持有的哈希上下文。</param>
    /// <param name="physicalKey">待验证的物理缓存键。</param>
    /// <param name="encodedPrefix">已转义的逻辑键前缀。</param>
    /// <param name="key">输出的逻辑键。</param>
    /// <returns>验证成功时返回 <see langword="true"/>，并设置逻辑键；否则返回 <see langword="false"/>。</returns>
    /// <remarks>前缀不匹配时会在解码和哈希计算前跳过该键。</remarks>
    internal static bool TryGetLogicalKeyWithPrefix(HashContext hashContext, string physicalKey,
        string encodedPrefix, out string key)
    {
        if (string.IsNullOrEmpty(encodedPrefix))
        {
            key = null;
            return false;
        }
        return TryGetLogicalKeyCore(hashContext, physicalKey, encodedPrefix, out key);
    }

    /// <summary>
    /// 验证并还原物理缓存键。
    /// </summary>
    /// <param name="hashContext">调用方持有的哈希上下文。</param>
    /// <param name="physicalKey">待验证的物理缓存键。</param>
    /// <param name="encodedPrefix">可选的已转义逻辑键前缀。</param>
    /// <param name="key">输出的逻辑键。</param>
    /// <returns>验证成功时返回 <see langword="true"/>。</returns>
    private static bool TryGetLogicalKeyCore(HashContext hashContext, string physicalKey,
        string encodedPrefix, out string key)
    {
        key = null;
        if (hashContext == null || physicalKey == null)
            return false;
        var configuration = hashContext.Configuration;
        var prefix = GetNamespacePrefix(configuration);
        if (!physicalKey.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var tagStart = prefix.Length + 1;
        var encodedStart = tagStart + 16 * 2 + 2;
        if (physicalKey.Length <= encodedStart || physicalKey[prefix.Length] != '{' ||
            physicalKey[tagStart + 16 * 2] != '}' || physicalKey[encodedStart - 1] != ':')
            return false;
        if (encodedPrefix != null &&
            (physicalKey.Length - encodedStart < encodedPrefix.Length ||
             string.CompareOrdinal(physicalKey, encodedStart, encodedPrefix, 0, encodedPrefix.Length) != 0))
            return false;
        string candidate;
        if (!TryGetCanonicalAsciiCandidate(physicalKey, encodedStart, out candidate))
        {
            var encoded = physicalKey.Substring(encodedStart);
            candidate = Uri.UnescapeDataString(encoded);
            if (string.IsNullOrWhiteSpace(candidate))
                return false;
            var canonical = Uri.EscapeDataString(candidate);
            if (canonical.Length != encoded.Length ||
                string.CompareOrdinal(canonical, 0, encoded, 0, canonical.Length) != 0)
                return false;
        }

        var hash = hashContext.ComputeHash(candidate);
        for (var index = 0; index < 16; index++)
        {
            if (physicalKey[tagStart + index * 2] != HexDigits[hash[index] >> 4] ||
                physicalKey[tagStart + index * 2 + 1] != HexDigits[hash[index] & 0x0f])
                return false;
        }
        key = candidate;
        return true;
    }

    /// <summary>
    /// 判断物理键是否属于给定逻辑前缀。
    /// </summary>
    /// <param name="hashContext">调用方持有的哈希上下文。</param>
    /// <param name="physicalKey">待检查的物理缓存键。</param>
    /// <param name="logicalPrefix">逻辑键前缀。</param>
    /// <returns>物理键属于该前缀时返回 <see langword="true"/>。</returns>
    /// <remarks>规范编码路径先比较编码前缀并校验哈希；其他输入回退到完整逻辑键解析。</remarks>
    internal static bool TryMatchLogicalPrefix(HashContext hashContext, string physicalKey, string logicalPrefix)
    {
        if (hashContext == null || physicalKey == null || logicalPrefix == null)
            return false;

        return TryMatchLogicalPrefix(hashContext, physicalKey, logicalPrefix,
            GetEncodedLogicalPrefix(logicalPrefix));
    }

    /// <summary>
    /// 判断物理键是否属于给定逻辑前缀。
    /// </summary>
    /// <param name="hashContext">调用方持有的哈希上下文。</param>
    /// <param name="physicalKey">待检查的物理缓存键。</param>
    /// <param name="logicalPrefix">逻辑键前缀。</param>
    /// <param name="encodedPrefix">预先生成的转义前缀。</param>
    /// <returns>物理键属于该前缀时返回 <see langword="true"/>。</returns>
    /// <remarks>复用预先生成的前缀，避免逐键编码逻辑前缀。</remarks>
    internal static bool TryMatchLogicalPrefix(HashContext hashContext, string physicalKey, string logicalPrefix,
        string encodedPrefix)
    {
        if (hashContext == null || physicalKey == null || logicalPrefix == null ||
            string.IsNullOrEmpty(encodedPrefix))
            return false;

        var configuration = hashContext.Configuration;
        var namespacePrefix = GetNamespacePrefix(configuration);
        var tagStart = namespacePrefix.Length + 1;
        var encodedStart = tagStart + 16 * 2 + 2;
        if (!HasPhysicalLayout(namespacePrefix, physicalKey, tagStart, encodedStart))
            return false;

        if (physicalKey.Length - encodedStart < encodedPrefix.Length ||
            string.CompareOrdinal(physicalKey, encodedStart, encodedPrefix, 0, encodedPrefix.Length) != 0)
            return false;

        var hash = hashContext.ComputeHashEncodedSuffix(physicalKey, encodedStart);
        if (hash == null)
            return TryGetLogicalKey(hashContext, physicalKey, out var decoded) &&
                   decoded.StartsWith(logicalPrefix, StringComparison.Ordinal);
        return HashMatches(physicalKey, tagStart, hash);
    }

    /// <summary>
    /// 检查物理键是否具有缓存键格式。
    /// </summary>
    /// <param name="namespacePrefix">物理命名空间前缀。</param>
    /// <param name="physicalKey">待检查的物理缓存键。</param>
    /// <param name="tagStart">hash tag 起始位置。</param>
    /// <param name="encodedStart">编码后缀起始位置。</param>
    /// <returns>格式有效时返回 <see langword="true"/>。</returns>
    private static bool HasPhysicalLayout(string namespacePrefix, string physicalKey, int tagStart,
        int encodedStart)
    {
        return physicalKey.StartsWith(namespacePrefix, StringComparison.Ordinal) &&
               physicalKey.Length > encodedStart && physicalKey[namespacePrefix.Length] == '{' &&
               physicalKey[tagStart + 16 * 2] == '}' && physicalKey[encodedStart - 1] == ':';
    }

    /// <summary>
    /// 比较物理键中的 hash tag 与计算得到的哈希值。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="tagStart">hash tag 起始位置。</param>
    /// <param name="hash">逻辑键哈希值。</param>
    /// <returns>哈希值匹配时返回 <see langword="true"/>。</returns>
    private static bool HashMatches(string physicalKey, int tagStart, byte[] hash)
    {
        for (var index = 0; index < 16; index++)
        {
            if (physicalKey[tagStart + index * 2] != HexDigits[hash[index] >> 4] ||
                physicalKey[tagStart + index * 2 + 1] != HexDigits[hash[index] & 0x0f])
                return false;
        }
        return true;
    }

    /// <summary>
    /// 将大写十六进制字符转换为数值。
    /// </summary>
    /// <param name="character">待转换的字符。</param>
    /// <returns>十六进制数值；无效字符返回 -1。</returns>
    private static int HexValue(char character)
    {
        if (character >= '0' && character <= '9')
            return character - '0';
        if (character >= 'A' && character <= 'F')
            return character - 'A' + 10;
        return -1;
    }

    /// <summary>
    /// 尝试直接读取规范 ASCII 物理键后缀。
    /// </summary>
    /// <param name="physicalKey">物理缓存键。</param>
    /// <param name="encodedStart">编码后缀起始位置。</param>
    /// <param name="candidate">输出的逻辑键候选值。</param>
    /// <returns>后缀为规范 ASCII 且非空白时返回 <see langword="true"/>。</returns>
    private static bool TryGetCanonicalAsciiCandidate(string physicalKey, int encodedStart,
        out string candidate)
    {
        candidate = null;
        if (physicalKey.Length <= encodedStart)
            return false;
        for (var index = encodedStart; index < physicalKey.Length; index++)
        {
            var character = physicalKey[index];
            if (character >= CanonicalAscii.Length || character == '%' || !CanonicalAscii[character])
                return false;
        }
        candidate = physicalKey.Substring(encodedStart);
        return !string.IsNullOrWhiteSpace(candidate);
    }

    /// <summary>
    /// 获取缓存实例的物理命名空间前缀。
    /// </summary>
    /// <param name="configuration">缓存键配置。</param>
    /// <returns>物理命名空间前缀。</returns>
    private static string GetNamespacePrefix(CacheConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        return configuration.PhysicalKeyPrefix;
    }

    /// <summary>
    /// 转义 Redis glob 模式中的特殊字符。
    /// </summary>
    /// <param name="value">待转义的模式片段。</param>
    /// <returns>转义后的模式片段。</returns>
    private static string EscapeRedisGlob(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var first = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (!IsRedisGlobCharacter(value[index]))
                continue;
            first = index;
            break;
        }
        if (first < 0)
            return value;

        var builder = new StringBuilder(value.Length + 4);
        builder.Append(value, 0, first);
        for (var index = first; index < value.Length; index++)
        {
            var character = value[index];
            if (IsRedisGlobCharacter(character))
                builder.Append('\\');
            builder.Append(character);
        }
        return builder.ToString();
    }

    /// <summary>
    /// 判断字符是否为 Redis glob 元字符。
    /// </summary>
    /// <param name="character">待检查的字符。</param>
    /// <returns>字符为 glob 元字符时返回 <see langword="true"/>。</returns>
    private static bool IsRedisGlobCharacter(char character) =>
        character == '*' || character == '?' || character == '[' || character == ']' || character == '\\';
}
