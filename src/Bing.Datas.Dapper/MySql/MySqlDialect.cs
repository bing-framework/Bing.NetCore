using Bing.Datas.Sql.Builders.Core;

namespace Bing.Datas.Dapper.MySql
{
    /// <summary>
    /// MySql方言
    /// </summary>
    public class MySqlDialect:DialectBase
    {
        /// <summary>
        /// 获取参数前缀
        /// </summary>
        /// <returns></returns>
        public override string GetPrefix()
        {
            return "@";
        }

        /// <summary>
        /// 闭合字符-开
        /// </summary>
        public override char OpenQuote => '`';

        /// <summary>
        /// 闭合字符-闭
        /// </summary>
        public override char CloseQuote => '`';

        /// <summary>
        /// 批量操作分隔符
        /// </summary>
        public override char BatchSeperator => ';';
    }
}
