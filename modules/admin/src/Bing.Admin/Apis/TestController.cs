﻿using System;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Jobs;
using Bing.Admin.Service.Abstractions;
using Bing.Admin.Systems.Domain.Events;
using Bing.AspNetCore.Mvc;
using Bing.Events.Messages;
using Bing.ExceptionHandling;
using Bing.Exceptions;
using Bing.Logging;
using DotNetCore.CAP.Internal;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Bing.Admin.Apis;

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
    /// 日志组件
    /// </summary>
    public ILogger<TestController> Logger { get; }

    /// <summary>
    /// 其它日志组件
    /// </summary>
    public ILog<TestController> OtherLog { get; }

    /// <summary>
    /// 初始化一个<see cref="TestController"/>类型的实例
    /// </summary>
    public TestController(ITestService testService
        , IProcessingServer processingServer
        , IMessageEventBus messageEventBus
        , IAdminUnitOfWork unitOfWork
        , IExceptionNotifier exceptionNotifier
        , ILogger<TestController> logger
        , ILog<TestController> otherLog)
    {
        TestService = testService;
        ProcessingServer = processingServer;
        MessageEventBus = messageEventBus;
        UnitOfWork = unitOfWork;
        ExceptionNotifier = exceptionNotifier;
        Logger = logger;
        OtherLog = otherLog;
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

    /// <summary>
    /// 测试释放CAP资源
    /// </summary>
    [AllowAnonymous]
    [HttpPost("testDisposed")]
    public Task<IActionResult> TestDisposed()
    {
        //var temp = ServiceLocator.Instance.GetService<IProcessingServer>();
        //temp.Dispose();
        ProcessingServer.Dispose();
        return Task.FromResult(Success());
    }

    /// <summary>
    /// 测试消息
    /// </summary>
    /// <param name="request">请求</param>
    [AllowAnonymous]
    [HttpPost("testMessage")]
    public async Task<IActionResult> TestMessageAsync([FromBody] TestMessage request)
    {
        Logger.LogInformation($"测试Logger消息{nameof(ILogger<TestController>)} - 0: {request.Id}");
        OtherLog
            .Message($"测试Logger消息{nameof(ILog<TestController>)} - 0: {request.Id}")
            .LogInformation();
        await MessageEventBus.PublishAsync(new TestMessageEvent1(request, request.Send));
        if (request.NeedCommit)
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

    /// <summary>
    /// 测试日志
    /// </summary>
    [AllowAnonymous]
    [HttpGet("testLogger")]
    public Task<IActionResult> TestLoggerAsync(string text)
    {
        Logger.LogTrace($"输出调试信息: {text}");
        Logger.LogTrace("输出调试信息[参数化]: {text}", text);
        OtherLog
            .Message("输出调试信息[参数化]: {text}", text)
            .Tags(nameof(TestController), "OtherLog", "LogTrace")
            .LogTrace();

        Logger.LogDebug($"输出调试信息: {text}");
        Logger.LogDebug("输出调试信息[参数化]: {text}", text);
        OtherLog
            .Message("输出调试信息[参数化]: {text}", text)
            .Tags(nameof(TestController), "OtherLog", "LogDebug")
            .LogDebug();

        Logger.LogInformation($"输出调试信息: {text}");
        Logger.LogInformation("输出调试信息[参数化]: {text}", text);
        OtherLog
            .Message("输出调试信息[参数化]: {text}", text)
            .Tags(nameof(TestController), "OtherLog", "LogInformation")
            .LogInformation();

        Logger.LogWarning($"输出调试信息: {text}");
        Logger.LogWarning("输出调试信息[参数化]: {text}", text);
        OtherLog
            .Message("输出调试信息[参数化]: {text}", text)
            .Tags(nameof(TestController), "OtherLog", "LogWarning")
            .LogWarning();
        return Task.FromResult(Success());
    }

    /// <summary>
    /// 测试日志属性
    /// </summary>
    [AllowAnonymous]
    [HttpGet("testLoggerWithProperties")]
    public Task<IActionResult> TestLoggerWithPropertiesAsync(string text)
    {
        var request = new TestNullableGuidRequest();
        request.TestId = Guid.NewGuid();
        request.TestId2 = Guid.NewGuid();
        OtherLog.Message("测试消息: {TestId}, {TestId2}, {text}", request.TestId, request.TestId2, text)
            .LogInformation();
        Logger.LogInformation("原生日志测试消息: {TestId}, {TestId2}, {text}", request.TestId, request.TestId2, text);

        return Task.FromResult(Success());
    }

    /// <summary>
    /// 测试异常自定义数据
    /// </summary>
    [AllowAnonymous]
    [HttpPost("testWarningWithData")]
    public Task<IActionResult> TestWarningWithDataAsync()
    {
        var ex = new BingFrameworkException("TestWarningWithData");
        ex.ExtraData.Add("TestA", "A");
        ex.ExtraData.Add("TestB", "ABC");
        ex.ExtraData.Add("TestC", new { A = 1, B = 2, C = 3 });
        throw ex;
    }

    /// <summary>
    /// 测试异常自定义内部数据
    /// </summary>
    [AllowAnonymous]
    [HttpPost("testWarningWithInternalData")]
    public Task<IActionResult> TestWarningWithInternalDataAsync()
    {
        var ex = new Warning("TestWarningWithInternalData");
        ex.Data.Add("TestA", "A");
        ex.Data.Add("TestB", "ABC");
        ex.Data.Add("TestC", new { A = 1, B = 2, C = 3 });
        throw ex;
    }

    /// <summary>
    /// 测试GUID
    /// </summary>
    /// <param name="id"></param>
    [AllowAnonymous]
    [HttpGet("testGuid")]
    public IActionResult TestGuid(Guid id)
    {
        return Success(id);
    }

    /// <summary>
    /// 测试 GUID 模型
    /// </summary>
    /// <param name="request"></param>
    [AllowAnonymous]
    [HttpGet("testGuidModel")]
    public IActionResult TestGuidModel([FromQuery] TestNullableGuidRequest request)
    {
        return Success(request.TestId);
    }

    /// <summary>
    /// 测试作业
    /// </summary>
    [AllowAnonymous]
    [HttpPost("testJob")]
    public IActionResult TestJob()
    {
        BackgroundJob.Enqueue<IDebugLogJob>(x => x.WriteLog());
        return Success();
    }

    /// <summary>
    /// 测试空GUID请求
    /// </summary>
    public class TestNullableGuidRequest
    {
        /// <summary>
        /// 测试ID1
        /// </summary>
        public Guid? TestId { get; set; }

        /// <summary>
        /// 测试ID2
        /// </summary>
        public Guid? TestId2 { get; set; }
    }
}
