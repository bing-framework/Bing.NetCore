using Bing.Datas.Sql.Builders.Core;

namespace Bing.Datas.Dapper.PgSql
{
    /// <summary>
    /// PgSql方言
    /// </summary>
    public class PgSqlDialect:DialectBase
    {
        /// <summary>
        /// 闭合字符-开
        /// </summary>
        public override char OpenQuote => '"';

        /// <summary>
        /// 闭合字符-闭
        /// </summary>
        public override char CloseQuote => '"';

        /// <summary>
        /// 批量操作分隔符
        /// </summary>
        public override char BatchSeperator => ';';
    }
}
