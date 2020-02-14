using System;
using System.Text.RegularExpressions;

// ReSharper disable once CheckNamespace
namespace Bing.Extensions
{
    /// <summary>
    /// 正则表达式(<see cref="Regex"/>) 扩展
    /// </summary>
    public static partial class RegexExtensions
    {
        /// <summary>
        /// 获取分组值
        /// </summary>
        /// <param name="match">Match</param>
        /// <param name="group">分组</param>
        public static string GetGroupValue(this Match match, string group)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (string.IsNullOrWhiteSpace(group))
                throw new ArgumentNullException(nameof(group));
            var g = match.Groups[group];
            if (!match.Success || !g.Success)
                throw new InvalidOperationException($"未能在匹配结果中找到匹配分组({group})");
            return g.Value;
        }
    }
}
