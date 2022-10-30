using System.Threading.Tasks;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Domain.Entities.Events;

public class DomainEventHandlerTest : IntegrationTestBase
{
    internal class EmployeeCreatedEventHandler1 : IDomainEventHandler<EntityCreatedEvent<Employee>>
    {
        public static string FirstName { get; private set; }

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="event">领域事件</param>
        public Task HandleAsync(EntityCreatedEvent<Employee> @event)
        {
            var employee = @event.Entity;
            FirstName = employee.FirstName;
            return Task.CompletedTask;
        }
    }

    internal class EmployeeCreatedEventHandler2 : IDomainEventHandler<EntityCreatedEvent<Employee>>
    {
        public static string LastName { get; private set; }

        /// <summary>
        /// 处理
        /// </summary>
        /// <param name="event">领域事件</param>
        public Task HandleAsync(EntityCreatedEvent<Employee> @event)
        {
            var employee = @event.Entity;
            LastName = employee.LastName;
            return Task.CompletedTask;
        }
    }

    private readonly IDomainEventDispatcher _dispatcher;

    public DomainEventHandlerTest()
    {
        _dispatcher = ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddDomainEventDispatcher();
    }

    [Fact]
    public async Task Handle_SubscribeMultipleEventHandlers_TriggerAllHandlers()
    {
        var employee = new Employee("Allen", "Yeager", "1");
        var @event = new EntityCreatedEvent<Employee>(employee);
        await _dispatcher.DispatchAsync(@event);
        Assert.Equal(employee.FirstName, EmployeeCreatedEventHandler1.FirstName);
        Assert.Equal(employee.LastName, EmployeeCreatedEventHandler2.LastName);
    }
}

public class EntityCreatedEvent<TEntity> : DomainEvent
{
    public TEntity Entity { get; }

    public EntityCreatedEvent(TEntity entity)
    {
        Entity = entity;
    }
}