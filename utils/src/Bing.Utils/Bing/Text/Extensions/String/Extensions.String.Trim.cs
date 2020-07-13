using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Bing.Text
{
    /// <summary>
    /// 字符串(<see cref="string"/>) 扩展
    /// </summary>
    public static partial class StringExtensions
    {
        /// <summary>
        /// 修剪空白字符
        /// </summary>
        /// <param name="texts">文本</param>
        public static void TrimAll(this IList<string> texts)
        {
            for (var i = 0; i < texts.Count; i++)
                texts[i] = texts[i].Trim();
        }

        /// <summary>
        /// 修剪按指定字符串
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="phrase">待修剪字词组</param>
        public static string TrimPhrase(this string text, string phrase)
        {
            var res = TrimPhraseStart(text, phrase);
            res = TrimPhraseEnd(res, phrase);
            return res;
        }

        /// <summary>
        /// 修剪头部按指定字符串
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="phrase">待修剪词组</param>
        public static string TrimPhraseStart(this string text, string phrase)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (string.IsNullOrEmpty(phrase))
                return text;
            while (text.StartsWith(phrase))
                text = text.Substring(phrase.Length);
            return text;
        }

        /// <summary>
        /// 修剪尾部按指定字符串
        /// </summary>
        /// <param name="text">文本</param>
        /// <param name="phrase">待修剪词组</param>
        public static string TrimPhraseEnd(this string text, string phrase)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (string.IsNullOrEmpty(phrase))
                return text;
            while (text.EndsWithIgnoreCase(phrase))
                text = text.Substring(0, text.Length - phrase.Length);
            return text;
        }
    }
}
