using System.Threading.Tasks;
using Bing.Admin.Service.Abstractions;
using Bing.AspNetCore.Mvc;
using Bing.DependencyInjection;
using DotNetCore.CAP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Admin.Apis
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    public class TestController : ApiControllerBase
    {
        /// <summary>
        /// 测试服务
        /// </summary>
        public ITestService TestService { get; }

        public IProcessingServer ProcessingServer { get; }

        /// <summary>
        /// 初始化一个<see cref="TestController"/>类型的实例
        /// </summary>
        /// <param name="testService">测试服务</param>
        public TestController(ITestService testService, IProcessingServer processingServer)
        {
            TestService = testService;
            ProcessingServer = processingServer;
        }

        /// <summary>
        /// 测试批量插入
        /// </summary>
        [HttpPost("testBatchInsert")]
        public async Task<IActionResult> TestBatchInsertAsync([FromBody]long qty)
        {
            await TestService.BatchInsertFileAsync(qty);
            return Success();
        }

        [AllowAnonymous]
        [HttpPost("testDisposed")]
        public async Task<IActionResult> TestDisposed()
        {
            //var temp = ServiceLocator.Instance.GetService<IProcessingServer>();
            //temp.Dispose();
            ProcessingServer.Dispose();
            return Success();
        }
    }
}
