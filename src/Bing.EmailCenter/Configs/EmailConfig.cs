using System;
using System.Collections.Generic;
using System.Text;

namespace Bing.EmailCenter.Configs
{
    /// <summary>
    /// 邮件配置
    /// </summary>
    public class EmailConfig
    {
        /// <summary>
        /// 显示授权
        /// </summary>
        public bool DisableOAuth { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 主机
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// 睡眠间隔。默认：3秒
        /// </summary>
        public int SleepInterval { get; set; } = 3000;
    }
}
