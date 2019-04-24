using System;
using Bing.Exceptions;

namespace Bing.Configurations
{
    /// <summary>
    /// Bing 框架配置
    /// </summary>
    public class BingConfig
    {
        /// <summary>
        /// 框架配置实例
        /// </summary>
        private static BingConfig _instance;

        /// <summary>
        /// 对象锁
        /// </summary>
        private static object _lockObj = new object();

        /// <summary>
        /// 当前框架配置
        /// </summary>
        public static BingConfig Current
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        _instance = new BingConfig();
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 验证
        /// </summary>
        public Action<string> ValidationHandler = (message) => throw new Warning(message);

        /// <summary>
        /// 是否启用用户名，用于设置审计创建人以及修改人
        /// </summary>
        public bool EnabledUserName { get; set; } = false;

        /// <summary>
        /// 是否启用调试日志
        /// </summary>
        public bool EnabledDebug { get; set; } = true;

        /// <summary>
        /// 是否启用跟踪日志
        /// </summary>
        public bool EnabledTrace { get; set; } = true;
    }
}
