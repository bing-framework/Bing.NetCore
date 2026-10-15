using Bing.Data.Events.Ordering;
using Bing.Events.Ordering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证 CAP 有序事务注册入口的幂等行为。
/// </summary>
public sealed class CapOrderedEventTransactionRegistrationTest
{
    /// <summary>
    /// 重复注册同一底层工厂时只保留一个适配器描述。
    /// </summary>
    [Fact]
    public void AddBingCapOrderedEventTransaction_WhenCalledTwice_ShouldRegisterOnce()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddBingCapOrderedEventTransaction<TestTransactionFactory>());
        services.AddBingCapOrderedEventTransaction<TestTransactionFactory>();

        Assert.Single(services, item => item.ServiceType == typeof(IOrderedEventTransactionFactory));
        Assert.Single(services, item => item.ServiceType == typeof(TestTransactionFactory));
    }

    /// <summary>
    /// 不同底层工厂不能覆盖同一服务集合中的 CAP 有序事务适配器。
    /// </summary>
    [Fact]
    public void AddBingCapOrderedEventTransaction_WhenDifferentFactoryIsRegistered_ShouldRejectWithoutPartialRegistration()
    {
        var services = new ServiceCollection();

        services.AddBingCapOrderedEventTransaction<TestTransactionFactory>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddBingCapOrderedEventTransaction<AlternateTransactionFactory>());

        Assert.Contains(typeof(TestTransactionFactory).FullName, exception.Message);
        Assert.Contains(typeof(AlternateTransactionFactory).FullName, exception.Message);
        Assert.Single(services, item => item.ServiceType == typeof(IOrderedEventTransactionFactory));
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(AlternateTransactionFactory));
    }

    /// <summary>
    /// 服务集合为空时拒绝注册。
    /// </summary>
    [Fact]
    public void AddBingCapOrderedEventTransaction_WhenServicesIsNull_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CapOrderedEventTransactionRegistrationExtensions
                .AddBingCapOrderedEventTransaction<TestTransactionFactory>(null));
    }

    /// <summary>
    /// 测试用底层事务工厂。
    /// </summary>
    private sealed class TestTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <inheritdoc />
        public Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IOrderedEventTransaction>(new NotSupportedException());
    }

    /// <summary>
    /// 用于验证重复适配器注册的另一底层事务工厂。
    /// </summary>
    private sealed class AlternateTransactionFactory : IOrderedEventTransactionFactory
    {
        /// <inheritdoc />
        public Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<IOrderedEventTransaction>(new NotSupportedException());
    }
}
