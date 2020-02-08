using System;
using System.Text.RegularExpressions;

namespace Bing.Utils.Judgments
{
    /// <summary>
    /// Guid判断
    /// </summary>
    public static class GuidJudgment
    {
        /// <summary>
        /// 判断 <see cref="Guid"/> 是否为空、null或Guid.Empty
        /// </summary>
        /// <param name="guid">Guid</param>
        public static bool IsNullOrEmpty(Guid guid) => guid == Guid.Empty;

        /// <summary>
        /// 判断 <see cref="Guid"/> 是否为空、null或Guid.Empty
        /// </summary>
        /// <param name="guid">Guid</param>
        public static bool IsNullOrEmpty(Guid? guid) => guid is null || IsNullOrEmpty(guid.Value);

        /// <summary>
        /// Guid 架构正则
        /// </summary>
        private static readonly Regex GuidSchema = new Regex("^[A-Fa-f0-9]{32}$|" +
                                                             "^({|\\()?[A-Fa-f0-9]{8}-([A-Fa-f0-9]{4}-){3}[A-Fa-f0-9]{12}(}|\\))?$|" +
                                                             "^({)?[0xA-Fa-f0-9]{3,10}(, {0,1}[0xA-Fa-f0-9]{3,6}){2},{0,1}({)([0xA-Fa-f0-9]{3,4}, {0,1}){7}[0xA-Fa-f0-9]{3,4}(}})$");

        /// <summary>
        /// 判断 字符串是否有效<see cref="Guid"/>
        /// </summary>
        /// <param name="guidStr">Guid字符串</param>
        public static bool IsValid(string guidStr) => !string.IsNullOrWhiteSpace(guidStr) && GuidSchema.Match(guidStr).Success;
    }
}
