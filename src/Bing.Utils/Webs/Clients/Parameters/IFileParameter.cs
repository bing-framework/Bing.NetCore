using System;
using System.IO;

namespace Bing.Utils.Webs.Clients.Parameters
{
    /// <summary>
    /// 文件参数
    /// </summary>
    public interface IFileParameter:IDisposable
    {
        /// <summary>
        /// 获取文件流
        /// </summary>
        /// <returns></returns>
        Stream GetFileStream();

        /// <summary>
        /// 获取文件名称
        /// </summary>
        /// <returns></returns>
        string GetFileName();

        /// <summary>
        /// 获取参数名
        /// </summary>
        /// <returns></returns>
        string GetName();
    }
}
