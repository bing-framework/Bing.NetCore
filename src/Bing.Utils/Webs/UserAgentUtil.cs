namespace Bing.Utils.Webs
{
    /// <summary>
    /// UserAgent操作辅助类
    /// </summary>
    public static class UserAgentUtil
    {
        #region GetOperatingSystemName(根据 UserAgent 获取操作系统名称)

        /// <summary>
        /// 根据 UserAgent 获取操作系统名称
        /// </summary>
        /// <param name="userAgent">UA</param>
        /// <returns></returns>
        public static string GetOperatingSystemName(string userAgent)
        {
            if (userAgent.Contains("NT 10.0"))
            {
                return "Windows 10";
            }
            if (userAgent.Contains("NT 6.2"))
            {
                return "Windows 8";
            }
            if (userAgent.Contains("NT 6.1 "))
            {
                return "Windows 7";
            }
            if (userAgent.Contains("NT 6.0"))
            {
                return "Windows Vista/Server 2008";
            }
            if (userAgent.Contains("NT 5.2"))
            {
                return "Windows Server 2003";
            }
            if (userAgent.Contains("NT 5.1"))
            {
                return "Windows XP";
            }
            if (userAgent.Contains("NT 5.0"))
            {
                return "Windows 2000";
            }
            if (userAgent.Contains("ME"))
            {
                return "Windows ME";
            }
            if (userAgent.Contains("Mac"))
            {
                return "Mac";
            }
            if (userAgent.Contains("Unix"))
            {
                return "UNIX";
            }
            if (userAgent.Contains("Linux"))
            {
                return "Linux";
            }
            if (userAgent.Contains("SunOS"))
            {
                return "Solaris";
            }
            if (userAgent.Contains("FreeBSD"))
            {
                return "FreeBSD";
            }
            return "Other OperationSystem";
        }

        #endregion

        #region GetBrowserName(根据 UserAgent 获取浏览器名称)

        /// <summary>
        /// 根据 UserAgent 获取浏览器名称
        /// </summary>
        /// <param name="userAgent">UA</param>
        /// <returns></returns>
        public static string GetBrowserName(string userAgent)
        {
            if (userAgent.Contains("Maxthon"))
            {
                return "遨游浏览器";
            }
            if (userAgent.Contains("MetaSr"))
            {
                return "搜狗高速浏览器";
            }
            if (userAgent.Contains("BIDUBrowser"))
            {
                return "百度浏览器";
            }
            if (userAgent.Contains("QQBrowser"))
            {
                return "QQ浏览器";
            }
            if (userAgent.Contains("GreenBrowser"))
            {
                return "Green浏览器";
            }
            if (userAgent.Contains("360se"))
            {
                return "360安全浏览器";
            }
            if (userAgent.Contains("MSIE 6.0"))
            {
                return "Internet Explorer 6.0";
            }
            if (userAgent.Contains("MSIE 7.0"))
            {
                return "Internet Explorer 7.0";
            }
            if (userAgent.Contains("MSIE 8.0"))
            {
                return "Internet Explorer 8.0";
            }
            if (userAgent.Contains("MSIE 9.0"))
            {
                return "Internet Explorer 9.0";
            }
            if (userAgent.Contains("MSIE 10.0"))
            {
                return "Internet Explorer 10.0";
            }
            if (userAgent.Contains("Firefox"))
            {
                return "Firefox";
            }
            if (userAgent.Contains("Opera"))
            {
                return "Opera";
            }
            if (userAgent.Contains("Chrome"))
            {
                return "Chrome";
            }
            if (userAgent.Contains("Safari"))
            {
                return "Safari";
            }
            return "Other Browser";
        }

        #endregion

        #region IsWechatBrowser(是否微信浏览器)

        /// <summary>
        /// 是否微信浏览器
        /// </summary>
        /// <param name="userAgent">UA</param>
        /// <returns></returns>
        public static bool IsWechatBrowser(string userAgent)
        {
            if (userAgent.Contains("MicroMessenger"))
            {
                return true;
            }

            return false;
        }

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
