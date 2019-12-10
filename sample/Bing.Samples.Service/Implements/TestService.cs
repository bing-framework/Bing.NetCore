using System;
using System.Threading.Tasks;
using Bing.Applications;
using Bing.Logs.Aspects;
using Bing.Samples.Service.Abstractions;

namespace Bing.Samples.Service.Implements
{
    /// <summary>
    /// 测试服务
    /// </summary>
    public class TestService : ServiceBase, ITestService
    {
        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="id">标识</param>
        [DebugLog]
        public async Task<string> GetAsync(Guid id)
        {
            return id.ToString();
        }
    }
}
