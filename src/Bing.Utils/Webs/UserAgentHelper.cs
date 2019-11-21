using System.Collections.Generic;

namespace Bing.Utils.Webs
{
    /// <summary>
    /// UserAgent操作辅助类
    /// </summary>
    public static class UserAgentHelper
    {
        /// <summary>
        /// 操作系统字典
        /// </summary>
        public static IDictionary<string, string> OperationSystemDict { get; set; } = new Dictionary<string, string>()
        {
            {"NT 10.0","Windows 10" },
            {"NT 6.2","Windows 8" },
            {"NT 6.1","Windows 7" },
            {"NT 6.0","Windows Vista/Server 2008" },
            {"NT 5.2","Windows Server 2003" },
            {"NT 5.1","Windows XP" },
            {"NT 5.0","Windows 2000" },
            {"ME","Windows ME" },
            {"Mac","Mac" },
            {"Unix","UNIX" },
            {"Linux","Linux" },
            {"SunOs","Solaris" },
            {"FreeBSD","FreeBSD" },
        };

        public static IDictionary<string,string> BrowserDict { get; set; }=new Dictionary<string, string>()
        {
            {"Maxthon","遨游浏览器" },
            {"MetaSr","搜狗高速浏览器" },
            {"BIDUBrowser","百度浏览器" },
            {"QQBrowser","QQ浏览器" },
            {"GreenBrowser","Green浏览器" },
            {"360se","360安全浏览器" },
            {"MSIE 6.0","Internet Explorer 6.0" },
            {"MSIE 7.0","Internet Explorer 7.0" },
            {"MSIE 8.0","Internet Explorer 8.0" },
            {"MSIE 9.0","Internet Explorer 9.0" },
            {"MSIE 10.0","Internet Explorer 10.0" },
            {"Firefox","Firefox" },
            {"Opera","Opera" },
            {"Chrome","Chrome" },
            {"Safari","Safari" },
        };

        #region GetOperatingSystemName(根据 UserAgent 获取操作系统名称)

        /// <summary>
        /// 根据 UserAgent 获取操作系统名称
        /// </summary>
        /// <param name="userAgent">UA</param>
        public static string GetOperatingSystemName(string userAgent)
        {
            foreach (var keyValue in OperationSystemDict)
            {
                if (userAgent.Contains(keyValue.Key))
                    return keyValue.Value;
            }
            return "Other OperationSystem";
        }

        #endregion

        #region GetBrowserName(根据 UserAgent 获取浏览器名称)

        /// <summary>
        /// 根据 UserAgent 获取浏览器名称
        /// </summary>
        /// <param name="userAgent">UA</param>
        public static string GetBrowserName(string userAgent)
        {
            foreach (var keyValue in BrowserDict)
            {
                if (userAgent.Contains(keyValue.Key))
                    return keyValue.Value;
            }
            return "Other Browser";
        }

        #endregion

        #region IsWechatBrowser(是否微信浏览器)

        /// <summary>
        /// 是否微信浏览器
        /// </summary>
        /// <param name="userAgent">UA</param>
        public static bool IsWechatBrowser(string userAgent) => userAgent.Contains("MicroMessenger");

        #endregion
    }

    /// <summary>
    /// 用户代理信息
    /// 参考地址：https://github.com/mumuy/browser/blob/master/Browser.js
    /// </summary>
    public class UserAgentInfo
    {
        /// <summary>
        /// 浏览器
        /// </summary>
        public string Browser { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 内核
        /// </summary>
        public string Engine { get; set; }

        /// <summary>
        /// 操作系统
        /// </summary>
        public string Os { get; set; }

        /// <summary>
        /// 操作系统版本号
        /// </summary>
        public string OsVersion { get; set; }

        /// <summary>
        /// 设备
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// 语言
        /// </summary>
        public string Language { get; set; }
    }
}
