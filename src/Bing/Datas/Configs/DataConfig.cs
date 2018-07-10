using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.Datas.Configs
{
    /// <summary>
    /// 数据配置
    /// </summary>
    public class DataConfig
    {
        /// <summary>
        /// 数据日志级别
        /// </summary>
        public static DataLogLevel LogLevel { get; set; } = DataLogLevel.Sql;

        /// <summary>
        /// 自动提交，默认禁用
        /// </summary>
        public static bool AutoCommit { get; set; } = false;

        /// <summary>
        /// 是否启用验证版本号，默认启用
        /// </summary>
        public static bool EnabledValidateVersion { get; set; } = true;

        /// <summary>
        /// ADO日志拦截器，
        /// </summary>
        public static Action<string, string, object> AdoLogInterceptor { get; set; } = null;

        /// <summary>
        /// 是否启用逻辑删除过滤
        /// </summary>
        public static bool EnabledDeleteFilter { get; set; } = true;
    }
}
