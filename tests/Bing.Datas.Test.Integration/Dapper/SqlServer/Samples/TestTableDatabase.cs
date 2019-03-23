using Bing.Datas.Sql.Matedatas;

namespace Bing.Datas.Test.Integration.Dapper.SqlServer.Samples
{
    /// <summary>
    /// 表数据库
    /// </summary>
    public class TestTableDatabase:ITableDatabase
    {
        /// <summary>
        /// 获取数据库
        /// </summary>
        /// <param name="table">表</param>
        /// <returns></returns>
        public string GetDatabase(string table)
        {
            return "test";
        }
    }
}
