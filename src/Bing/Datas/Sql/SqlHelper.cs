using System;
using System.Collections.Generic;
using System.Text;
using Bing.Utils.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Datas.Sql
{
    /// <summary>
    /// Sql辅助操作
    /// </summary>
    public class SqlHelper
    {
        /// <summary>
        /// 获取参数字面值
        /// </summary>
        /// <param name="value">参数值</param>
        /// <returns></returns>
        public static string GetParamLiterals(object value)
        {
            if (value == null)
            {
                return "''";
            }
            switch (value.GetType().Name.ToLower())
            {
                case "boolean":
                    return Conv.ToBool(value) ? "1" : "0";
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
