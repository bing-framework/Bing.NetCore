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
        /// 是否大写字符串
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool IsUpper(this string text) => text.All(ch => char.IsLetter(ch) && !char.IsLower(ch));

        /// <summary>
        /// 是否小写字符串
        /// </summary>
        /// <param name="text">字符串</param>
        public static bool IsLower(this string text) => text.All(ch => !char.IsLetter(ch) || !char.IsUpper(ch));

        /// <summary>
        /// 转换为首字母大写字符串
        /// </summary>
        /// <param name="original">字符串</param>
        public static string ToCapitalCase(this string original)
        {
            var words = original.Split(' ');
            var result = new List<string>();
            foreach (var word in words)
            {
                if(word.Length==0||AllCapitals(word))
                    result.Add(word);
                else if (word.Length == 1)
                    result.Add(word.ToUpper());
                else
                    result.Add(char.ToUpper(word[0]) + word.Remove(0, 1).ToLower());
            }

            return string.Join(" ", result)
                .Replace(" Y ", " y ")
                .Replace(" De ", " de ")
                .Replace(" O ", " o ");
        }

        /// <summary>
        /// 是否全部大写
        /// </summary>
        /// <param name="input">输入值</param>
        private static bool AllCapitals(string input) => input.ToCharArray().All(char.IsUpper);

        /// <summary>
        /// 转换为首字母小写字符串
        /// </summary>
        /// <param name="original">字符串</param>
        public static string ToCamelCase(this string original)
        {
            if (original.Length <= 1)
                return original.ToLower();
            return char.ToLower(original[0]) + original.Substring(1);
        }
    }
}
