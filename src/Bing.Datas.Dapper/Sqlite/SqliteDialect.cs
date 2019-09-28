using Bing.Datas.Sql.Builders.Core;

namespace Bing.Datas.Dapper.Sqlite
{
    /// <summary>
    /// Sqlite方言
    /// </summary>
    public class SqliteDialect:DialectBase
    {
        /// <summary>
        /// 起始转义标识符
        /// </summary>
        public override char OpeningIdentifier => '`';

        /// <summary>
        /// 结束转义标识符
        /// </summary>
        public override char ClosingIdentifier => '`';
    }
}
