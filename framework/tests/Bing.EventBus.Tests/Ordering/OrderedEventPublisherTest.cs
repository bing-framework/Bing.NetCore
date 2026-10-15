using Bing.Events.Cap;
using Bing.Events.Ordering;
using DotNetCore.CAP;
using Moq;
using Xunit;
using CurrentMessageEvent = Bing.Events.Messages.MessageEvent;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序发布入口的事务身份与 CAP 元数据。
/// </summary>
public sealed class OrderedEventPublisherTest
{
    /// <summary>
    /// 验证真实数据库序号随 CAP 消息头发布且只调用一次。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenCapTransactionMatches_ShouldAllocateAndPublishOnce()
    {
        using var database = new OrderingSqliteFixture();
        await using var transaction = await database.CreateFactory().BeginAsync();
        var ambient = new AsyncLocal<ICapTransaction>();
        var capTransaction = new Mock<ICapTransaction>();
        capTransaction.SetupAllProperties();
        capTransaction.Object.DbTransaction = transaction.Transaction;
        ambient.Value = capTransaction.Object;
        var cap = new Mock<ICapPublisher>();
        cap.SetupGet(value => value.Transaction).Returns(ambient);
        IDictionary<string, string> captured = null;
        cap.Setup(value => value.PublishAsync("inventory.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, IDictionary<string, string>, CancellationToken>((_, _, headers, _) =>
                captured = new Dictionary<string, string>(headers))
            .Returns(Task.CompletedTask);
        var publisher = new OrderedEventPublisher(database.CreateStore(), cap.Object);
        var message = Message("first");
        message.Callback = "callback";

        await publisher.PublishAsync(transaction.Transaction, message);
        await transaction.CommitAsync();

        Assert.Equal(1, message.Sequence);
        Assert.Equal(1, await database.ReadSequenceAsync(OrderingSqliteFixture.Stream()));
        Assert.NotNull(captured);
        var metadata = ClusterEventHeaders.Read(new Dictionary<string, string>(captured));
        Assert.Equal("first", metadata.EventId);
        Assert.Equal(1, metadata.Sequence);
        Assert.Equal("tenant", metadata.TenantId);
        Assert.Equal("inventory", metadata.Stream);
        Assert.Equal("sku", metadata.EntityKey);
        Assert.Equal("v1", metadata.ContractVersion);
        Assert.Equal("trace", metadata.TraceId);
        Assert.Equal("callback", captured[DotNetCore.CAP.Messages.Headers.CallbackName]);
        cap.Verify(value => value.PublishAsync("inventory.changed", message.Data,
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证 CAP 事务包装数据库事务时仍能进行有序发布。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenCapTransactionWrapsDatabaseTransaction_ShouldAllocateAndPublish()
    {
        using var database = new OrderingSqliteFixture();
        await using var transaction = await database.CreateFactory().BeginAsync();
        var capTransaction = new Mock<ICapTransaction>();
        capTransaction.SetupAllProperties();
        capTransaction.Object.DbTransaction = new TransactionWrapper(transaction.Transaction);
        var ambient = new AsyncLocal<ICapTransaction> { Value = capTransaction.Object };
        var cap = new Mock<ICapPublisher>();
        cap.SetupGet(value => value.Transaction).Returns(ambient);
        cap.Setup(value => value.PublishAsync("inventory.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await new OrderedEventPublisher(database.CreateStore(), cap.Object)
            .PublishAsync(transaction.Transaction, Message("wrapped"));
        await transaction.CommitAsync();

        Assert.Equal(1, await database.ReadSequenceAsync(OrderingSqliteFixture.Stream()));
    }

    /// <summary>
    /// 验证 CAP 发布失败后整笔事务回滚不留下序号缺口。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenCapFailsAndCallerRollsBack_ShouldReuseSequence()
    {
        using var database = new OrderingSqliteFixture();
        var ambient = new AsyncLocal<ICapTransaction>();
        var cap = new Mock<ICapPublisher>();
        cap.SetupGet(value => value.Transaction).Returns(ambient);
        var original = new InvalidOperationException("CAP publish failed");
        cap.Setup(value => value.PublishAsync("inventory.changed", It.IsAny<object>(),
                It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(original);
        var publisher = new OrderedEventPublisher(database.CreateStore(), cap.Object);

        await using (var transaction = await database.CreateFactory().BeginAsync())
        {
            var capTransaction = new Mock<ICapTransaction>();
            capTransaction.SetupAllProperties();
            capTransaction.Object.DbTransaction = transaction.Transaction;
            ambient.Value = capTransaction.Object;
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                publisher.PublishAsync(transaction.Transaction, Message("failed")));
            Assert.Same(original, failure);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await database.ReadSequenceAsync(OrderingSqliteFixture.Stream()));
        await using var retry = await database.CreateFactory().BeginAsync();
        Assert.Equal(1, await database.CreateStore().AllocateSequenceAsync(retry.Transaction,
            OrderingSqliteFixture.Stream()));
        await retry.CommitAsync();
    }

    /// <summary>
    /// 验证事务身份不匹配及重复分配时不会推进序号。
    /// </summary>
    [Fact]
    public async Task PublishAsync_WhenTransactionIsUnrelatedOrMessageAllocated_ShouldRejectBeforeWrite()
    {
        using var database = new OrderingSqliteFixture();
        await using var transaction = await database.CreateFactory().BeginAsync();
        var ambient = new AsyncLocal<ICapTransaction>();
        var cap = new Mock<ICapPublisher>();
        cap.SetupGet(value => value.Transaction).Returns(ambient);
        var publisher = new OrderedEventPublisher(database.CreateStore(), cap.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(transaction.Transaction, Message("wrong-transaction")));
        var alreadyAllocated = Message("allocated");
        alreadyAllocated.Sequence = 1;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            publisher.PublishAsync(transaction.Transaction, alreadyAllocated));

        await transaction.RollbackAsync();
        Assert.Equal(0, await database.ReadSequenceAsync(OrderingSqliteFixture.Stream()));
        cap.Verify(value => value.PublishAsync(It.IsAny<string>(), It.IsAny<object>(),
            It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 创建带完整有序元数据的测试消息。
    /// </summary>
    private static CurrentMessageEvent Message(string id) => new()
    {
        Id = id,
        Name = "inventory.changed",
        Data = id,
        TenantId = "tenant",
        Stream = "inventory",
        EntityKey = "sku",
        ContractVersion = "v1",
        TraceId = "trace"
    };

    /// <summary>
    /// 为测试模拟 ORM 对数据库事务的包装。
    /// </summary>
    private sealed class TransactionWrapper
    {
        /// <summary>
        /// 被测试包装的数据库事务。
        /// </summary>
        private readonly System.Data.Common.DbTransaction _transaction;

        /// <summary>
        /// 初始化一个 <see cref="TransactionWrapper" /> 类型的实例。
        /// </summary>
        public TransactionWrapper(System.Data.Common.DbTransaction transaction)
        {
            _transaction = transaction;
        }

        /// <summary>
        /// 返回包装的数据库事务。
        /// </summary>
        public System.Data.Common.DbTransaction GetDbTransaction() => _transaction;
    }
}
