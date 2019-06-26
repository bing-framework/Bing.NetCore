using Bing.Datas.Sql.Builders;
using Bing.Datas.Sql.Builders.Core;
using Bing.Datas.Sql.Matedatas;

namespace Bing.Datas.Dapper.Oracle
{
    /// <summary>
    /// Sql项
    /// </summary>
    public class OracleSqlItem : SqlItem
    {
        /// <summary>
        /// 初始化一个<see cref="OracleSqlItem"/>类型的实例
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="prefix">前缀</param>
        /// <param name="alias">别名</param>
        /// <param name="raw">使用原始值</param>
        /// <param name="isSplit">是否用句点分割名称</param>
        public OracleSqlItem(string name, string prefix = null, string alias = null, bool raw = false, bool isSplit = true) : base(name, prefix, alias, raw, isSplit)
        {
        }

        /// <summary>
        /// 获取列名和别名
        /// </summary>
        /// <param name="dialect">Sql方言</param>
        /// <param name="tableDatabase">表数据库</param>
        protected override string GetColumnAlias(IDialect dialect, ITableDatabase tableDatabase) => $"{GetColumn(dialect, tableDatabase)} {GetSafeName(dialect, Alias)}";
    }
}
