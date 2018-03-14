using System;
using System.Collections.Generic;
using System.Text;
using Bing.Exceptions;

namespace Bing.Configuration
{
    /// <summary>
    /// Bing 框架配置
    /// </summary>
    public class BingConfig
    {
        /// <summary>
        /// 验证
        /// </summary>
        public Action<string> ValidationHandler = (message) => throw new Warning(message);

        /// <summary>
        /// 是否启用用户名，用于设置审计创建人以及修改人
        /// </summary>
        public bool EnabledUserName { get; set; } = false;
    }
}
