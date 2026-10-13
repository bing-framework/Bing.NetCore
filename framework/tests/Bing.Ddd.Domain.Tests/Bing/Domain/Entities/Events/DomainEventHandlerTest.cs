using System.Threading.Tasks;
using Bing.Test.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Domain.Entities.Events;

/// <summary>
/// 验证领域事件处理器的注册与派发。
/// </summary>
public class DomainEventHandlerTest : IntegrationTestBase
{
    /// <summary>
    /// 处理员工创建事件并记录员工名。
    /// </summary>
    internal class EmployeeCreatedEventHandler1 : IDomainEventHandler<EntityCreatedEvent<Employee>>
    {
        /// <summary>
        /// 获取或设置处理事件中的员工名。
        /// </summary>
        public static string FirstName { get; private set; }

        /// <inheritdoc />
        public Task HandleAsync(EntityCreatedEvent<Employee> @event)
        {
            var employee = @event.Entity;
            FirstName = employee.FirstName;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 处理员工创建事件并记录员工姓氏。
    /// </summary>
    internal class EmployeeCreatedEventHandler2 : IDomainEventHandler<EntityCreatedEvent<Employee>>
    {
        /// <summary>
        /// 获取或设置处理事件中的员工姓氏。
        /// </summary>
        public static string LastName { get; private set; }

        /// <inheritdoc />
        public Task HandleAsync(EntityCreatedEvent<Employee> @event)
        {
            var employee = @event.Entity;
            LastName = employee.LastName;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 用于验证领域事件处理器调用的事件分发器。
    /// </summary>
    private readonly IDomainEventDispatcher _dispatcher;

    /// <summary>
    /// 初始化一个 <see cref="DomainEventHandlerTest"/> 类型的实例。
    /// </summary>
    public DomainEventHandlerTest()
    {
        _dispatcher = ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
    }

    /// <inheritdoc />
    protected override void ConfigureServices(IServiceCollection services)
    {
        services.AddDomainEventDispatcher();
    }

    /// <summary>
    /// 验证多个事件处理器均会收到派发的领域事件。
    /// </summary>
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

/// <summary>
/// 表示实体创建领域事件。
/// </summary>
/// <typeparam name="TEntity">实体类型。</typeparam>
public class EntityCreatedEvent<TEntity> : DomainEvent
{
    /// <summary>
    /// 获取创建的实体。
    /// </summary>
    public TEntity Entity { get; }

    /// <summary>
    /// 初始化一个 <see cref="EntityCreatedEvent{TEntity}"/> 类型的实例。
    /// </summary>
    /// <param name="entity">创建的实体。</param>
    public EntityCreatedEvent(TEntity entity)
    {
        Entity = entity;
    }
}
