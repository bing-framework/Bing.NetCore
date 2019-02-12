namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// Sql方言基类
    /// </summary>
    public abstract class DialectBase:IDialect
    {
        /// <summary>
        /// 安全名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }
            if (name == "*")
            {
                return name;
            }
            name = name.Trim().TrimStart(OpenQuote).TrimEnd(CloseQuote);
            return $"{OpenQuote}{name}{CloseQuote}";
        }

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        /// <returns></returns>
        public abstract string GetPrefix();

        /// <summary>
        /// 闭合字符-开
        /// </summary>
        public abstract char OpenQuote { get; }

        /// <summary>
        /// 闭合字符-闭
        /// </summary>
        public abstract char CloseQuote { get; }

        /// <summary>
        /// 批量操作分隔符
        /// </summary>
        public abstract char BatchSeperator { get; }
    }
}
