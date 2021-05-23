using Bing.Validations.Abstractions;
using Bing.Validations.Handlers;

namespace Bing.Configuration
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
        /// 验证处理器
        /// </summary>
        public IValidationHandler ValidationHandler { get; set; } = new ThrowHandler();

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
