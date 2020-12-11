using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing.Admin.Service.Abstractions
{
    /// <summary>
    /// 测试 服务
    /// </summary>
    public interface ITestService : Bing.Application.Services.IAppService
    {
        /// <summary>
        /// 批量插入文件
        /// </summary>
        Task BatchInsertFileAsync(long qty);

        /// <summary>
        /// 测试参数空异常
        /// </summary>
        Task TestArgumentNullAsync(List<string> list);
    }
}
