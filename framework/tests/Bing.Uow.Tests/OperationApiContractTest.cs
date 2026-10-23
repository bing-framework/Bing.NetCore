using System.Reflection;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Uow.Tests;

/// <summary>
/// 验证业务操作 opt-in 注册对外可用。
/// </summary>
public class OperationApiContractTest
{
    /// <summary>
    /// 验证冻结的 operation API 签名及旧 scope manager 入口保持不变。
    /// </summary>
    [Fact]
    public void OperationContracts_ShouldMatchFrozenSignaturesAndRetainLegacyScopeApi()
    {
        typeof(IUnitOfWorkExecutionScope).GetInterfaces().ShouldContain(typeof(IUnitOfWorkScope));
        typeof(IUnitOfWorkExecutionScope).GetProperty(nameof(IUnitOfWorkExecutionScope.CancellationToken))!
            .PropertyType.ShouldBe(typeof(CancellationToken));

        var beginOperation = GetMethod(typeof(IUnitOfWorkOperationManager), nameof(IUnitOfWorkOperationManager.BeginOperation),
            0, typeof(UnitOfWorkScopeOptions), typeof(CancellationToken));
        beginOperation.ReturnType.ShouldBe(typeof(IUnitOfWorkExecutionScope));
        beginOperation.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(new[]
        {
            typeof(UnitOfWorkScopeOptions), typeof(CancellationToken)
        });

        var saveHandler = typeof(IUnitOfWorkSaveChangesHandler);
        GetMethod(saveHandler, nameof(IUnitOfWorkSaveChangesHandler.SaveChanges), 0,
            typeof(IUnitOfWorkScope)).ReturnType.ShouldBe(typeof(void));
        GetMethod(saveHandler, nameof(IUnitOfWorkSaveChangesHandler.SaveChangesAsync), 0,
            typeof(IUnitOfWorkScope), typeof(CancellationToken)).ReturnType.ShouldBe(typeof(Task));

        var executeVoid = GetGenericMethod(typeof(IBingOperationExecutor),
            nameof(IBingOperationExecutor.ExecuteAsync), 1);
        executeVoid.GetParameters().Length.ShouldBe(3);
        var serviceType = executeVoid.GetGenericArguments()[0];
        executeVoid.GetParameters()[0].ParameterType.ShouldBe(
            typeof(Func<,,>).MakeGenericType(serviceType, typeof(CancellationToken), typeof(Task)));
        executeVoid.ReturnType.ShouldBe(typeof(Task));
        executeVoid.GetGenericArguments()[0].GenericParameterAttributes.HasFlag(
            GenericParameterAttributes.ReferenceTypeConstraint).ShouldBeTrue();

        var executeResult = GetGenericMethod(typeof(IBingOperationExecutor),
            nameof(IBingOperationExecutor.ExecuteAsync), 2);
        executeResult.GetParameters().Length.ShouldBe(3);
        var genericArguments = executeResult.GetGenericArguments();
        executeResult.GetParameters()[0].ParameterType.ShouldBe(typeof(Func<,,>).MakeGenericType(
            genericArguments[0], typeof(CancellationToken), typeof(Task<>).MakeGenericType(genericArguments[1])));
        executeResult.ReturnType.ShouldBe(typeof(Task<>).MakeGenericType(genericArguments[1]));
        genericArguments[0].GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint)
            .ShouldBeTrue();

        typeof(UnitOfWorkScopeOptions).GetProperty(nameof(UnitOfWorkScopeOptions.Timeout))!.PropertyType
            .ShouldBe(typeof(TimeSpan?));
        GetMethod(typeof(BingOperationServiceCollectionExtensions), nameof(BingOperationServiceCollectionExtensions.AddBingOperations),
            0, typeof(IServiceCollection)).ReturnType.ShouldBe(typeof(IServiceCollection));

        typeof(IUnitOfWorkScopeManager).GetProperty(nameof(IUnitOfWorkScopeManager.Current))!.PropertyType
            .ShouldBe(typeof(IUnitOfWorkScope));
        GetMethod(typeof(IUnitOfWorkScopeManager), nameof(IUnitOfWorkScopeManager.Begin), 0,
            typeof(UnitOfWorkScopeOptions), typeof(bool)).ReturnType.ShouldBe(typeof(IUnitOfWorkScope));
    }

    /// <summary>
    /// 验证显式启用后，操作管理器与执行器可以从同一服务容器解析。
    /// </summary>
    [Fact]
    public void AddBingOperations_ShouldRegisterOperationManagerAndExecutor()
    {
        var services = new ServiceCollection();
        services.AddBingOperations();
        services.AddBingOperations();

        services.Count(descriptor => descriptor.ServiceType == typeof(IUnitOfWorkScopeManager)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IUnitOfWorkOperationManager)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IBingOperationExecutor)).ShouldBe(1);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScopeManager>();
        scope.ServiceProvider.GetRequiredService<IUnitOfWorkOperationManager>().ShouldBeSameAs(scopeManager);
        provider.GetRequiredService<IBingOperationExecutor>().ShouldBeSameAs(
            provider.GetRequiredService<IBingOperationExecutor>());
    }

    private static MethodInfo GetMethod(Type declaringType, string name, int genericArity,
        params Type[] parameterTypes) => declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.Static).Single(method => method.Name == name &&
            method.GetGenericArguments().Length == genericArity &&
            method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));

    private static MethodInfo GetGenericMethod(Type declaringType, string name, int genericArity) =>
        declaringType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Single(method =>
            method.Name == name && method.GetGenericArguments().Length == genericArity);
}
