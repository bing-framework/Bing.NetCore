using Bing.Datas.Sql.Builders;
using Bing.Extensions;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Dapper.PgSql
{
    /// <summary>
    /// PgSql参数字面值解析器
    /// </summary>
    public class PgSqlParamLiteralsResolver : IParamLiteralsResolver
    {
        /// <summary>
        /// 获取参数字面值
        /// </summary>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        public string GetParamLiterals(object value)
        {
            if (value == null)
                return "''";
            switch (value.GetType().Name.ToLower())
            {
                case "boolean":
                    return Conv.ToBool(value) ? "true" : "false";
                case "int16":
                case "int32":
                case "int64":
                case "single":
                case "double":
                case "decimal":
                    return value.SafeString();
                default:
                    return $"'{value}'";
            }
        }
    }
}
