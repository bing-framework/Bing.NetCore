namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// Sql方言基类
    /// </summary>
    public abstract class DialectBase:IDialect
    {
        /// <summary>
        /// 起始转义标识符
        /// </summary>
        public virtual char OpeningIdentifier { get; } = '[';

        /// <summary>
        /// 结束转义标识符
        /// </summary>
        public virtual char ClosingIdentifier { get; } = ']';

        /// <summary>
        /// 批量操作分隔符
        /// </summary>
        public virtual char BatchSeperator { get; } = ';';

        /// <summary>
        /// 安全名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public virtual string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }
            if (name == "*")
            {
                return name;
            }

            return GetSafeName(FilterName(name));
        }

        /// <summary>
        /// 过滤名称
        /// </summary>
        /// <param name="name">明后才能</param>
        /// <returns></returns>
        protected string FilterName(string name)
        {
            return name.Trim().TrimStart('[').TrimEnd(']').TrimStart('`').TrimEnd('`').TrimStart('"').TrimEnd('"');
        }

        /// <summary>
        /// 获取安全名称
        /// </summary>
        /// <param name="name">名称</param>
        /// <returns></returns>
        protected virtual string GetSafeName(string name)
        {
            return $"{OpeningIdentifier}{name}{ClosingIdentifier}";
        }

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        /// <returns></returns>
        public virtual string GetPrefix()
        {
            return "@";
        }

        
    }
}
