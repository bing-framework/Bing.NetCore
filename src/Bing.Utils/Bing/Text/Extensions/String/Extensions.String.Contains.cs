using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 包含
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="value">包含字符串</param>
        /// <param name="values">包含字符串数组</param>
        public static bool Contains(this string text, string value, params string[] values)
        {
            return YieldReturnStrings().Any(text.Contains);

            IEnumerable<string> YieldReturnStrings()
            {
                yield return value;
                if (value is null)
                    yield break;
                foreach (var val in values)
                    yield return val;
            }
        }

        /// <summary>
        /// 包含
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="character">包含字符</param>
        public static bool Contains(this string text, char character) => text.Any(c => c == character);

        /// <summary>
        /// 包含
        /// </summary>
        /// <param name="text">字符串</param>
        /// <param name="character">包含字符</param>
        /// <param name="characters">包含字符数组</param>
        public static bool Contains(this string text, char character, params char[] characters)
        {
            return YieldReturnCharacters().Any(text.Contains);

            IEnumerable<char> YieldReturnCharacters()
            {
                yield return character;
                if (characters is null)
                    yield break;
                foreach (var val in characters)
                    yield return val;
            }
        }
    }
}
