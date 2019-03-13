using System.Runtime.InteropServices;

namespace Bing.Utils.Helpers
{
    /// <summary>
    /// 系统操作
    /// </summary>
    public static class Sys
    {
        /// <summary>
        /// 是否Linux操作系统
        /// </summary>
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// 是否Windows操作系统
        /// </summary>
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// 是否苹果操作系统
        /// </summary>
        public static bool IsOsx => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// 当前操作系统
        /// </summary>
        public static string System => IsWindows ? "Windows" : IsLinux ? "Linux" : IsOsx ? "OSX" : string.Empty;
    }
}
