using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Bing.Admin.Data;
using Bing.Admin.Data.Inventory;
using Bing.Auditing;
using Bing.Auditing.Persistence;
using Bing.Data.Events.Ordering;
using Bing.Data.Inventory;
using Bing.Data.Transaction;
using Bing.Events.Cap;
using Bing.Events.Messages;
using Bing.Events.Ordering;
using DotNetCore.CAP;
using Moq;
using Xunit;

namespace Bing.Admin.InventoryTests;

/// <summary>
/// 验证库存服务发布兼容事件和有序事件时的事务边界。
/// </summary>
public sealed class AdminInventoryOrderedEventPublishingTest
{
    /// <summary>
    /// 有序发布器存在时应在同一库存事务中发布两个主题。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenOrderedPublisherIsPresent_ShouldPublishBothTopicsInOneTransaction()
    {
        var transaction = new Mock<DbTransaction>().Object;
        var actions = new TransactionActionManager();
        var unitOfWork = new Mock<IAdminUnitOfWork>();
        unitOfWork.Setup(value => value.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken cancellationToken) =>
            {
                await actions.CommitAsync(transaction);
                return 1;
            });

        var request = new InventoryOrderRequest("tenant-a", "warehouse-a", "order-a",
            new[] { new InventoryOrderLine("sku-a", 2) });
        var completed = new InventoryOrderResult(InventoryDeductionStatus.Applied, false, new long?[] { 8 });
        var orders = new Mock<IInventoryOrderStore>();
        DbTransaction callbackTransaction = null;
        orders.Setup(value => value.ExecuteAsync(transaction, request,
                It.IsAny<Func<DbTransaction, InventoryOrderResult, CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((DbTransaction current, InventoryOrderRequest _,
                Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> callback,
                CancellationToken cancellationToken) =>
            {
                callbackTransaction = current;
                return InvokeCompletedAsync(callback, current, completed, cancellationToken);
            });

        var auditing = new Mock<ITransactionalAuditingStore>();
        auditing.Setup(value => value.SaveAsync(transaction, It.IsAny<string>(), It.IsAny<AuditLogInfo>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var legacyEvents = new Mock<IMessageEventBus>();
        var legacyMessages = new List<MessageEvent<object>>();
        legacyEvents.Setup(value => value.PublishAsync(It.IsAny<MessageEvent<object>>(),
                It.IsAny<CancellationToken>()))
            .Callback<MessageEvent<object>, CancellationToken>((message, _) => legacyMessages.Add(message))
            .Returns(Task.CompletedTask);

        var orderedStore = new Mock<IOrderedEventStore>();
        DbTransaction allocatedTransaction = null;
        orderedStore.Setup(value => value.AllocateSequenceAsync(transaction,
                It.IsAny<OrderedEventStream>(), It.IsAny<CancellationToken>()))
            .Callback<DbTransaction, OrderedEventStream, CancellationToken>((current, _, _) =>
                allocatedTransaction = current)
            .ReturnsAsync(7L);

        var capTransaction = new Mock<ICapTransaction>();
        capTransaction.SetupAllProperties();
        capTransaction.Object.DbTransaction = transaction;
        var ambient = new AsyncLocal<ICapTransaction> { Value = capTransaction.Object };
        var cap = new Mock<ICapPublisher>();
        cap.SetupGet(value => value.Transaction).Returns(ambient);
        string orderedTopic = null;
        IDictionary<string, string> orderedHeaders = null;
        cap.Setup(value => value.PublishAsync(It.IsAny<string>(), It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, IDictionary<string, string>, CancellationToken>((name, _, headers, _) =>
            {
                orderedTopic = name;
                orderedHeaders = new Dictionary<string, string>(headers);
            })
            .Returns(Task.CompletedTask);

        var service = new AdminInventoryService(unitOfWork.Object, actions, () => transaction,
            orders.Object, auditing.Object, legacyEvents.Object,
            new OrderedEventPublisher(orderedStore.Object, cap.Object));

        var result = await service.ExecuteAsync(request, "user-a", "trace-a");

        Assert.True(result.Applied);
        Assert.Same(transaction, callbackTransaction);
        Assert.Same(transaction, allocatedTransaction);
        var legacyMessage = Assert.Single(legacyMessages);
        Assert.Equal(AdminInventoryService.CompletedTopic, legacyMessage.Name);
        Assert.Equal(AdminInventoryService.CompletedOrderedTopic, orderedTopic);
        var metadata = ClusterEventHeaders.Read(new Dictionary<string, string>(orderedHeaders));
        Assert.Equal(request.TenantId, metadata.TenantId);
        Assert.Equal(AdminInventoryService.OrderedStream, metadata.Stream);
        Assert.Equal(request.OrderId, metadata.EntityKey);
        Assert.Equal(7, metadata.Sequence);
        Assert.Equal("v1", metadata.ContractVersion);
        Assert.Equal("trace-a", metadata.TraceId);
    }

    /// <summary>
    /// 执行库存完成回调并返回固定结果。
    /// </summary>
    /// <param name="callback">库存完成回调。</param>
    /// <param name="transaction">当前事务。</param>
    /// <param name="result">固定库存结果。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>固定库存结果。</returns>
    private static async Task<InventoryOrderResult> InvokeCompletedAsync(
        Func<DbTransaction, InventoryOrderResult, CancellationToken, Task> callback,
        DbTransaction transaction, InventoryOrderResult result, CancellationToken cancellationToken)
    {
        await callback(transaction, result, cancellationToken);
        return result;
    }
}
