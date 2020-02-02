using Bing.Datas.Sql.Builders.Core;
using Bing.Helpers;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

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
        /// Select子句是否支持As关键字
        /// </summary>
        public override bool SupportSelectAs() => false;

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

        /// <summary>
        /// 获取参数值
        /// </summary>
        /// <param name="paramValue">参数值</param>
        public override object GetParamValue(object paramValue)
        {
            if (paramValue == null)
                return "";
            switch (paramValue.GetType().Name.ToLower())
            {
                case "boolean":
                    return Conv.ToBool(paramValue) ? 1 : 0;
                case "int16":
                case "int32":
                case "int64":
                case "single":
                case "double":
                case "decimal":
                    return paramValue.SafeString();
                default:
                    return $"{paramValue}";
            }
        }
    }
}
