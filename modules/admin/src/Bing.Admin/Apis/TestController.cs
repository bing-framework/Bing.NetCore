using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Systems.Domain.Events;
using Bing.AspNetCore.Mvc;
using Bing.DependencyInjection;
using Bing.Events.Messages;
using Bing.ExceptionHandling;
using DotNetCore.CAP;
using DotNetCore.CAP.Internal;
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

        /// <summary>
        /// 处理服务
        /// </summary>
        public IProcessingServer ProcessingServer { get; }

        /// <summary>
        /// 消息事件总线
        /// </summary>
        public IMessageEventBus MessageEventBus { get; }

        /// <summary>
        /// 工作单元
        /// </summary>
        public IAdminUnitOfWork UnitOfWork { get; }

        /// <summary>
        /// 异常通知器
        /// </summary>
        public IExceptionNotifier ExceptionNotifier { get; }

        /// <summary>
        /// 初始化一个<see cref="TestController"/>类型的实例
        /// </summary>
        public TestController(ITestService testService
            , IProcessingServer processingServer
            , IMessageEventBus messageEventBus
            , IAdminUnitOfWork unitOfWork
            , IExceptionNotifier exceptionNotifier)
        {
            TestService = testService;
            ProcessingServer = processingServer;
            MessageEventBus = messageEventBus;
            UnitOfWork = unitOfWork;
            ExceptionNotifier = exceptionNotifier;
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

        /// <summary>
        /// 测试消息
        /// </summary>
        /// <param name="request">请求</param>
        [AllowAnonymous]
        [HttpPost("testMessage")]
        public async Task<IActionResult> TestMessageAsync([FromBody] TestMessage request)
        {
            await MessageEventBus.PublishAsync(new TestMessageEvent1(request));
            await UnitOfWork.CommitAsync();
            return Success();
        }

        /// <summary>
        /// 测试参数空异常
        /// </summary>
        [AllowAnonymous]
        [HttpPost("testArgumentNull")]
        public async Task<IActionResult> TestArgumentNullAsync()
        {
            await TestService.TestArgumentNullAsync(null);
            return Success();
        }
    }
}
