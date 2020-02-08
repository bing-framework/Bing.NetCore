using System;

namespace Bing.SqlBuilder
{
    /// <summary>
    /// Sql生成器异常
    /// </summary>
    public class SqlBuilderException : Exception
    {
        /// <summary>
        /// 初始化一个<see cref="SqlBuilderException"/>类型的实例
        /// </summary>
        /// <param name="message"></param>
        public SqlBuilderException(string message) : base(message) { }
    }
}
