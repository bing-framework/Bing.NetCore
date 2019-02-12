namespace Bing.Datas.Sql.Builders
{
    /// <summary>
    /// Sql方言
    /// </summary>
    public interface IDialect
    {
        /// <summary>
        /// 安全名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        string SafeName(string name);

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        /// <returns></returns>
        string GetPrefix();

        /// <summary>
        /// 闭合字符-开
        /// </summary>
        char OpenQuote { get; }

        /// <summary>
        /// 闭合字符-闭
        /// </summary>
        char CloseQuote { get; }

        /// <summary>
        /// 批量操作分隔符
        /// </summary>
        char BatchSeperator { get; }
    }
}
