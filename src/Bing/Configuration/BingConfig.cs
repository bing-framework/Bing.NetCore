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
    }
}
