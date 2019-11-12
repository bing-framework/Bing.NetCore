using System;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Logs.Aspects;

namespace Bing.Samples.Service.Abstractions
{
    /// <summary>
    /// 测试服务
    /// </summary>
    public interface ITestService : IService
    {
        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="id">标识</param>
        [DebugLog]
        Task<string> GetAsync(Guid id);
    }
}
