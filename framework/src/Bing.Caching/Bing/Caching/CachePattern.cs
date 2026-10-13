using System.Text;
using System.Text.RegularExpressions;

namespace Bing.Caching;

/// <summary>
/// 将缓存逻辑键通配模式转换为完整匹配的正则表达式。
/// </summary>
public static class CachePattern
{
    /// <summary>
    /// 指定缓存 glob 模式中的令牌类型。
    /// </summary>
    internal enum TokenKind : byte
    {
        /// <summary>
        /// 表示字面字符。
        /// </summary>
        Literal,
        /// <summary>
        /// 表示匹配单个字符的通配符。
        /// </summary>
        Any,
        /// <summary>
        /// 表示匹配任意字符序列的通配符。
        /// </summary>
        Star
    }

    /// <summary>
    /// 表示缓存 glob 模式中的一个令牌。
    /// </summary>
    internal readonly struct Token
    {
        /// <summary>
        /// 初始化一个 <see cref="Token"/> 类型的实例。
        /// </summary>
        /// <param name="kind">令牌类型。</param>
        /// <param name="value">字面字符。</param>
        internal Token(TokenKind kind, char value = default)
        {
            Kind = kind;
            Value = value;
        }

        /// <summary>
        /// 获取令牌类型。
        /// </summary>
        internal TokenKind Kind { get; }
        /// <summary>
        /// 获取令牌对应的字面字符。
        /// </summary>
        internal char Value { get; }
    }

    /// <summary>
    /// 表示已解析的缓存模式匹配器。
    /// </summary>
    /// <remarks>
    /// 模式在创建时解析；匹配过程不创建正则表达式，也不会受到正则回溯影响。
    /// </remarks>
    internal sealed class Matcher
    {
        /// <summary>
        /// 解析后的模式令牌。
        /// </summary>
        private readonly Token[] _tokens;

        /// <summary>
        /// 初始化一个 <see cref="Matcher"/> 类型的实例。
        /// </summary>
        /// <param name="tokens">解析后的模式令牌。</param>
        internal Matcher(Token[] tokens)
        {
            _tokens = tokens;
            var prefix = new StringBuilder();
            var isLiteral = true;
            for (var index = 0; index < tokens.Length; index++)
            {
                if (tokens[index].Kind != TokenKind.Literal)
                {
                    isLiteral = false;
                    break;
                }
                prefix.Append(tokens[index].Value);
            }
            LiteralPrefix = prefix.ToString();
            IsLiteral = isLiteral;
        }

        /// <summary>
        /// 获取第一个通配符前的字面逻辑前缀。
        /// </summary>
        /// <remarks>没有安全前缀时返回空字符串。</remarks>
        internal string LiteralPrefix { get; }

        /// <summary>
        /// 获取模式是否只包含字面量。
        /// </summary>
        /// <remarks>字面量模式可以直接按单键删除。</remarks>
        internal bool IsLiteral { get; }

        /// <summary>
        /// 判断逻辑键是否匹配已解析的模式。
        /// </summary>
        /// <param name="input">待匹配的逻辑键。</param>
        /// <returns>匹配时返回 <see langword="true"/>。</returns>
        internal bool IsMatch(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var patternIndex = 0;
            var inputIndex = 0;
            var starIndex = -1;
            var starInputIndex = -1;
            while (inputIndex < input.Length)
            {
                if (patternIndex < _tokens.Length)
                {
                    var token = _tokens[patternIndex];
                    if (token.Kind == TokenKind.Literal && token.Value == input[inputIndex])
                    {
                        patternIndex++;
                        inputIndex++;
                        continue;
                    }
                    if (token.Kind == TokenKind.Any)
                    {
                        patternIndex++;
                        inputIndex++;
                        continue;
                    }
                    if (token.Kind == TokenKind.Star)
                    {
                        starIndex = patternIndex++;
                        starInputIndex = inputIndex;
                        continue;
                    }
                }

                if (starIndex < 0)
                    return false;
                patternIndex = starIndex + 1;
                inputIndex = ++starInputIndex;
            }

            while (patternIndex < _tokens.Length && _tokens[patternIndex].Kind == TokenKind.Star)
                patternIndex++;
            return patternIndex == _tokens.Length;
        }
    }

    /// <summary>
    /// 创建缓存 glob 模式匹配器。
    /// </summary>
    /// <param name="pattern">逻辑键模式。</param>
    /// <returns>已解析的模式匹配器。</returns>
    /// <remarks>模式只解析一次，适合逐项匹配扫描结果。</remarks>
    internal static Matcher CreateMatcher(string pattern) => new(Parse(pattern));

    /// <summary>
    /// 创建缓存逻辑键模式对应的正则表达式。
    /// </summary>
    /// <param name="pattern">逻辑键模式。</param>
    /// <returns>从字符串首尾进行完整匹配的正则表达式。</returns>
    /// <exception cref="ArgumentException">模式为空，或模式只包含未转义通配符。</exception>
    /// <remarks><c>*</c> 匹配零个或多个字符，<c>?</c> 匹配一个字符，<c>\</c> 转义下一个字符。</remarks>
    public static Regex CreateRegex(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("缓存键模式不能为空。", nameof(pattern));

        var expression = new StringBuilder("\\A");
        var escaped = false;
        var hasWildcard = false;
        var hasLiteral = false;

        foreach (var character in pattern)
        {
            if (escaped)
            {
                expression.Append(Regex.Escape(character.ToString()));
                escaped = false;
                hasLiteral = true;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '*')
            {
                expression.Append(".*");
                hasWildcard = true;
                continue;
            }

            if (character == '?')
            {
                expression.Append('.');
                hasWildcard = true;
                continue;
            }

            expression.Append(Regex.Escape(character.ToString()));
            hasLiteral = true;
        }

        if (escaped)
        {
            expression.Append(Regex.Escape("\\"));
            hasLiteral = true;
        }

        if (hasWildcard && !hasLiteral)
            throw new ArgumentException("缓存键模式不能匹配整个命名空间。", nameof(pattern));

        expression.Append("\\z");
        return new Regex(expression.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 判断逻辑键是否匹配缓存模式。
    /// </summary>
    /// <param name="input">待匹配的逻辑键。</param>
    /// <param name="pattern">逻辑键模式。</param>
    /// <returns>匹配返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
    public static bool IsMatch(string input, string pattern)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        return CreateMatcher(pattern).IsMatch(input);
    }

    /// <summary>
    /// 将逻辑键模式解析为令牌序列。
    /// </summary>
    /// <param name="pattern">逻辑键模式。</param>
    /// <returns>解析后的模式令牌。</returns>
    private static Token[] Parse(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("缓存键模式不能为空。", nameof(pattern));

        var tokens = new List<Token>(pattern.Length);
        var escaped = false;
        var hasWildcard = false;
        var hasLiteral = false;
        foreach (var character in pattern)
        {
            if (escaped)
            {
                tokens.Add(new Token(TokenKind.Literal, character));
                escaped = false;
                hasLiteral = true;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '*')
            {
                tokens.Add(new Token(TokenKind.Star));
                hasWildcard = true;
                continue;
            }

            if (character == '?')
            {
                tokens.Add(new Token(TokenKind.Any));
                hasWildcard = true;
                continue;
            }

            tokens.Add(new Token(TokenKind.Literal, character));
            hasLiteral = true;
        }

        if (escaped)
        {
            tokens.Add(new Token(TokenKind.Literal, '\\'));
            hasLiteral = true;
        }

        if (hasWildcard && !hasLiteral)
            throw new ArgumentException("缓存键模式不能匹配整个命名空间。", nameof(pattern));
        return tokens.ToArray();
    }
}
