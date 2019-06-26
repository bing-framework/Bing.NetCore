namespace Bing.Datas.Sql.Builders.Core
{
    /// <summary>
    /// Sql方言基类
    /// </summary>
    public abstract class DialectBase : IDialect
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
        public virtual string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            if (name == "*")
                return name;
            return GetSafeName(FilterName(name));
        }

        /// <summary>
        /// 过滤名称
        /// </summary>
        /// <param name="name">明后才能</param>
        protected string FilterName(string name) => name.Trim().TrimStart('[').TrimEnd(']').TrimStart('`').TrimEnd('`').TrimStart('"').TrimEnd('"');

        /// <summary>
        /// 获取安全名称
        /// </summary>
        /// <param name="name">名称</param>
        protected virtual string GetSafeName(string name) => $"{OpeningIdentifier}{name}{ClosingIdentifier}";

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        public virtual string GetPrefix() => "@";

        /// <summary>
        /// 生成参数名
        /// </summary>
        /// <param name="paramIndex">参数索引</param>
        public virtual string GenerateName(int paramIndex) => $"{GetPrefix()}_p_{paramIndex}";

        /// <summary>
        /// 获取参数名
        /// </summary>
        /// <param name="paramName">参数名</param>
        public virtual string GetParamName(string paramName) => paramName;
    }
}
