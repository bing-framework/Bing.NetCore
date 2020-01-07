using System;
using System.Threading.Tasks;
using Bing.Samples.Service.Abstractions;
using Bing.Webs.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bing.Samples.Controllers
{
    /// <summary>
    /// 测试 控制器
    /// </summary>
    public class TestController : ApiControllerBase
    {
        /// <summary>
        /// 初始化一个<see cref="TestController"/>类型的实例
        /// </summary>
        /// <param name="testService">测试服务</param>
        public TestController(ITestService testService)
        {
            TestService = testService;
        }

        /// <summary>
        /// 测试服务
        /// </summary>
        public ITestService TestService { get; set; }

        /// <summary>
        /// 获取字符串
        /// </summary>
        /// <param name="id">标识</param>
        [HttpGet("{id}")]
        public virtual async Task<IActionResult> GetAsync(Guid id)
        {
            var result = await TestService.GetAsync(id);
            return Success(result);
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        /// <param name="content">内容</param>
        [HttpPost("writeLog")]
        public async Task<IActionResult> WriteLogAsync([FromBody] string content)
        {
            await TestService.WriteLogAsync(content);
            return Success();
        }
    }
}
