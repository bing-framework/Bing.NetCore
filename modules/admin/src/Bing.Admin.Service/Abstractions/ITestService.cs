using System.Threading.Tasks;
using Bing.Applications;

namespace Bing.Admin.Service.Abstractions
{
    /// <summary>
    /// 测试 服务
    /// </summary>
    public interface ITestService : IService
    {
        /// <summary>
        /// 批量插入文件
        /// </summary>
        Task BatchInsertFileAsync(long qty);
    }
}
