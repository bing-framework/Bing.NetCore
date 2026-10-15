using System;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Apis.Events;
using Bing.AspNetCore.Mvc;
using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Admin.Tests.Shared;

/// <summary>
/// 验证 Admin 有序事件恢复控制器的启用边界和参数转发。
/// </summary>
public sealed class AdminOrderedEventRecoveryControllerTest
{
    /// <summary>
    /// 恢复服务未注册时返回 404。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenRecoveryIsDisabled_ShouldReturnNotFound()
    {
        var controller = ActivatorUtilities.CreateInstance<OrderedEventRecoveryController>(
            new ServiceCollection().BuildServiceProvider());
        controller.ControllerContext = CreateControllerContext();

        var result = await controller.GetStateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    /// <summary>
    /// 状态查询应将业务流和消费者完整交给恢复服务。
    /// </summary>
    [Fact]
    public async Task GetStateAsync_WhenEnabled_ShouldReturnState()
    {
        var service = new RecordingRecoveryService();
        var controller = CreateController(service);
        var request = CreateRequest();

        var result = await controller.GetStateAsync(request, CancellationToken.None);

        var response = Assert.IsType<ApiResult>(result);
        var state = Assert.IsType<OrderedEventStreamState>(response.Data);
        Assert.Equal(4, state.LastSequence);
        Assert.Equal("inventory", service.Consumer);
        Assert.Equal("tenant-a", service.Stream.TenantId);
        Assert.Equal("sku-1", service.Stream.EntityKey);
    }

    /// <summary>
    /// 人工重试应只调用恢复服务一次并返回成功结果。
    /// </summary>
    [Fact]
    public async Task RetryAsync_WhenEnabled_ShouldInvokeRecoveryService()
    {
        var service = new RecordingRecoveryService();
        var controller = CreateController(service);

        var result = await controller.RetryAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<ApiResult>(result);
        Assert.Equal(1, service.RetryCount);
        Assert.Equal("inventory", service.Consumer);
    }

    /// <summary>
    /// 请求转换应保留全局业务流的空租户语义。
    /// </summary>
    [Fact]
    public void ToStream_WhenTenantIsMissing_ShouldUseEmptyTenant()
    {
        var stream = new OrderedEventRecoveryRequest
        {
            Consumer = "inventory",
            Stream = "stock",
            EntityKey = "sku-1"
        }.ToStream();

        Assert.Equal(string.Empty, stream.TenantId);
        Assert.Equal("stock", stream.Stream);
    }

    /// <summary>
    /// 创建带有请求服务的控制器上下文。
    /// </summary>
    private static OrderedEventRecoveryController CreateController(IOrderedEventRecoveryService service)
    {
        var controller = new OrderedEventRecoveryController(service)
        {
            ControllerContext = CreateControllerContext()
        };
        return controller;
    }

    /// <summary>
    /// 创建控制器执行上下文。
    /// </summary>
    private static ControllerContext CreateControllerContext() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        }
    };

    /// <summary>
    /// 创建有效的恢复请求。
    /// </summary>
    private static OrderedEventRecoveryRequest CreateRequest() => new()
    {
        Consumer = "inventory",
        TenantId = "tenant-a",
        Stream = "stock",
        EntityKey = "sku-1"
    };

    /// <summary>
    /// 记录控制器转发参数的恢复服务替身。
    /// </summary>
    private sealed class RecordingRecoveryService : IOrderedEventRecoveryService
    {
        /// <summary>
        /// 获取记录的消费者标识。
        /// </summary>
        public string Consumer { get; private set; }

        /// <summary>
        /// 获取记录的业务流。
        /// </summary>
        public OrderedEventStream Stream { get; private set; }

        /// <summary>
        /// 获取请求的重试次数。
        /// </summary>
        public int RetryCount { get; private set; }

        /// <inheritdoc />
        public Task<OrderedEventStreamState> GetStateAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default)
        {
            Consumer = consumer;
            Stream = stream;
            return Task.FromResult(new OrderedEventStreamState(stream, 4, 5, 2, true, 1, "failed"));
        }

        /// <inheritdoc />
        public Task RetryAsync(string consumer, OrderedEventStream stream,
            CancellationToken cancellationToken = default)
        {
            Consumer = consumer;
            Stream = stream;
            RetryCount++;
            return Task.CompletedTask;
        }
    }
}
