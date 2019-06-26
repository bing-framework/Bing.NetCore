using Bing.Datas.Sql.Builders.Core;

namespace Bing.Datas.Dapper.Oracle
{
    /// <summary>
    /// Oracle方言
    /// </summary>
    public class OracleDialect : DialectBase
    {
        /// <summary>
        /// 起始转义标识符
        /// </summary>
        public override char OpeningIdentifier => '"';

        /// <summary>
        /// 结束转义标识符
        /// </summary>
        public override char ClosingIdentifier => '"';

        /// <summary>
        /// 获取参数前缀
        /// </summary>
        public override string GetPrefix() => ":";

        /// <summary>
        /// 生成参数名
        /// </summary>
        /// <param name="paramIndex">参数索引</param>
        public override string GenerateName(int paramIndex) => $"{GetPrefix()}p_{paramIndex}";

        /// <summary>
        /// 获取参数名
        /// </summary>
        /// <param name="paramName">参数名</param>
        public override string GetParamName(string paramName) => paramName.StartsWith(":") ? paramName.TrimStart(':') : paramName;
    }
}
