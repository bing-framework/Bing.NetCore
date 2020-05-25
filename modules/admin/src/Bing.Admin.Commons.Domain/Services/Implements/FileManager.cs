using Bing.Domains.Services;
using Bing.Admin.Commons.Domain.Models;
using Bing.Admin.Commons.Domain.Services.Abstractions;

namespace Bing.Admin.Commons.Domain.Services.Implements
{
    /// <summary>
    /// 文件 管理
    /// </summary>
    public class FileManager : DomainServiceBase, IFileManager
    {
        /// <summary>
        /// 初始化一个<see cref="FileManager"/>类型的实例
        /// </summary>
        public FileManager() { }
    }
}