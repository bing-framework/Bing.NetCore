using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AspectCore.DynamicProxy;
using AspectCore.DependencyInjection;
using AspectCore.Extensions.AspectScope;
using AspectCore.Extensions.DependencyInjection;
using AspectCore.Extensions.Reflection;
using Bing.Application.Aspects;
using Bing.Uow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Bing.DependencyInjection;

/// <summary>
/// 注册 Bing 工作单元的 AspectCore 适配器。
/// </summary>
public static class BingUnitOfWorkExtensions
{
    /// <summary>
    /// 显式启用标记方法的自动 owner/participant 工作单元边界。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">自动工作单元配置。</param>
    /// <returns>当前服务集合。</returns>
    public static IServiceCollection EnableBingAutomaticUnitOfWork(this IServiceCollection services,
        Action<BingAutomaticUnitOfWorkOptions> configure = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.AddBingOperations();
        services.AddOptions<BingAutomaticUnitOfWorkOptions>();
        if (configure != null)
            services.Configure(configure);
        services.TryAddSingleton<BingAutomaticUnitOfWorkRegistrationMarker>();
        return services.EnableBingUnitOfWork();
    }

    /// <summary>
    /// 启用中立工作单元标记的 AspectCore 执行器。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>当前服务集合。</returns>
    public static IServiceCollection EnableBingUnitOfWork(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (services.Any(descriptor => descriptor.ServiceType == typeof(BingUnitOfWorkRegistrationMarker)))
            return services;

        services.AddSingleton<BingUnitOfWorkRegistrationMarker>();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAdditionalInterceptorSelector, BingUnitOfWorkInterceptorSelector>());
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IServiceResolveCallback, BingUnitOfWorkProxyCallback>());
        services.ConfigureDynamicProxy(configuration =>
            configuration.ValidationHandlers.Add(new BingUnitOfWorkValidationHandler()));
        return services;
    }
}

/// <summary>
/// 标记工作单元适配器已经注册，避免重复追加动态代理配置。
/// </summary>
internal sealed class BingUnitOfWorkRegistrationMarker
{
}

/// <summary>
/// 标记容器启用了新的自动 owner/participant 边界。
/// </summary>
internal sealed class BingAutomaticUnitOfWorkRegistrationMarker
{
}

/// <summary>
/// 为仅在实现方法上声明中立工作单元标记且尚未生成代理的接口服务补充代理。
/// </summary>
internal sealed class BingUnitOfWorkProxyCallback : IServiceResolveCallback
{
    /// <inheritdoc />
    public object Invoke(IServiceResolver resolver, object instance, ServiceDefinition definition)
    {
        if (resolver == null || instance == null || definition?.ServiceType?.IsInterface != true)
            return instance;

        if (IsAspectCoreProxy(instance.GetType()) ||
            HasUnitOfWorkTypeMarker(definition.ServiceType, instance.GetType()) ||
            HasUnitOfWorkServiceMethodMarker(definition.ServiceType) ||
            HasUnitOfWorkImplementationMethodMarker(instance.GetType()) == false)
            return instance;

        var generator = resolver.Resolve(typeof(IProxyGenerator)) as IProxyGenerator;
        return generator?.CreateInterfaceProxy(definition.ServiceType, instance) ?? instance;
    }

    /// <summary>
    /// 判断服务接口或实现类型是否声明工作单元标记。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>存在工作单元标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkTypeMarker(Type serviceType, Type implementationType)
    {
        if (serviceType.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) ||
            implementationType.IsDefined(typeof(UnitOfWorkAttribute), inherit: true))
            return true;

        return false;
    }

    /// <summary>
    /// 判断服务契约是否声明工作单元方法标记。
    /// </summary>
    /// <param name="serviceType">服务接口类型。</param>
    /// <returns>存在方法标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkServiceMethodMarker(Type serviceType)
    {
        return serviceType.GetMethods().Any(method =>
            method.IsDefined(typeof(UnitOfWorkAttribute), inherit: true));
    }

    /// <summary>
    /// 判断实现类型是否声明工作单元方法标记。
    /// </summary>
    /// <param name="implementationType">实现类型。</param>
    /// <returns>存在方法标记时返回 <see langword="true" />。</returns>
    private static bool HasUnitOfWorkImplementationMethodMarker(Type implementationType)
    {
        return implementationType.GetMethods().Any(method =>
            method.IsDefined(typeof(UnitOfWorkAttribute), inherit: true));
    }

    /// <summary>
    /// 判断实例是否已由 AspectCore 生成代理。
    /// </summary>
    /// <param name="type">实例类型。</param>
    /// <returns>为 AspectCore 代理时返回 <see langword="true" />。</returns>
    private static bool IsAspectCoreProxy(Type type)
    {
        return type.Assembly.GetName().Name == "AspectCore.DynamicProxy.Generator" ||
            type.FullName?.StartsWith("AspectCore.DynamicGenerated.", StringComparison.Ordinal) == true;
    }
}

/// <summary>
/// 让包含中立工作单元标记的方法进入代理验证范围。
/// </summary>
internal sealed class BingUnitOfWorkValidationHandler : IAspectValidationHandler
{
    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool Invoke(AspectValidationContext context, AspectValidationDelegate next)
    {
        if (context.Method?.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) == true)
            return true;
        if (context.Method?.DeclaringType?.IsDefined(typeof(UnitOfWorkAttribute), inherit: true) == true)
            return true;
        return next(context);
    }
}

/// <summary>
/// 选择带有中立工作单元标记的方法拦截器。
/// </summary>
internal sealed class BingUnitOfWorkInterceptorSelector : IAdditionalInterceptorSelector
{
    /// <inheritdoc />
    public IEnumerable<IInterceptor> Select(MethodInfo serviceMethod, MethodInfo implementationMethod)
    {
        var attribute = FindAttribute(serviceMethod, implementationMethod);
        if (attribute == null)
            return Array.Empty<IInterceptor>();

        var returnType = implementationMethod?.ReturnType ?? serviceMethod?.ReturnType;
        if (IsValueTask(returnType))
        {
            return new IInterceptor[]
            {
                new BingUnitOfWorkValueTaskInterceptor(attribute),
                new BingUnitOfWorkInterceptor(attribute),
                new BingAutomaticUnitOfWorkInterceptor(attribute)
            };
        }

        return new IInterceptor[]
        {
            new BingUnitOfWorkInterceptor(attribute),
            new BingAutomaticUnitOfWorkInterceptor(attribute)
        };
    }

    private static bool IsValueTask(Type returnType) =>
        returnType == typeof(ValueTask) ||
        returnType?.IsGenericType == true && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>);

    /// <summary>
    /// 查找服务方法、实现方法或声明类型上的工作单元标记。
    /// </summary>
    /// <param name="serviceMethod">服务方法。</param>
    /// <param name="implementationMethod">实现方法。</param>
    /// <returns>找到的标记；未找到时返回 <see langword="null" />。</returns>
    private static UnitOfWorkAttribute FindAttribute(MethodInfo serviceMethod, MethodInfo implementationMethod)
    {
        var attribute = serviceMethod?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        attribute = implementationMethod?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        attribute = serviceMethod?.DeclaringType?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
        if (attribute != null)
            return attribute;

        return implementationMethod?.DeclaringType?.GetCustomAttributes<UnitOfWorkAttribute>(inherit: true).FirstOrDefault();
    }
}

/// <summary>
/// 执行工作单元提交的 AspectCore 拦截器。
/// </summary>
internal sealed class BingUnitOfWorkInterceptor : AbstractInterceptorAttribute, IScopeInterceptor
{
    private static readonly ConditionalWeakTable<IUnitOfWorkScope, CommitAfterServiceSet> CommitAfterServices = new();

    /// <summary>
    /// 初始化一个 <see cref="BingUnitOfWorkInterceptor" /> 类型的实例。
    /// </summary>
    /// <param name="attribute">中立工作单元标记。</param>
    public BingUnitOfWorkInterceptor(UnitOfWorkAttribute attribute)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));

        Order = attribute.Order;
        Scope = MapScope(attribute.Scope);
        Inherited = true;
    }

    /// <inheritdoc />
    public Scope Scope { get; set; }

    /// <inheritdoc />
    public override Task Invoke(AspectContext context, AspectDelegate next)
    {
        var adaptedContext = BingUnitOfWorkValueTaskInterceptor.AdaptContext(context);
        if (adaptedContext.ServiceProvider.GetService<BingAutomaticUnitOfWorkRegistrationMarker>() != null)
            return next(adaptedContext);

        var services = adaptedContext.ServiceProvider;
        var operationManager = services.GetService<IUnitOfWorkOperationManager>();
        if (services.GetService<IBingOperationExecutor>() != null &&
            operationManager?.Current is IUnitOfWorkExecutionScope { IsActive: true })
            return InvokeParticipant(adaptedContext, next, operationManager);

        return InvokeCoreAsync(adaptedContext, next(adaptedContext));
    }

    private static Task InvokeParticipant(AspectContext context, AspectDelegate next,
        IUnitOfWorkOperationManager operationManager)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = CompleteParticipantInvocationAsync(context, next, operationManager, completion);
        return completion.Task;
    }

    private static async Task CompleteParticipantInvocationAsync(AspectContext context, AspectDelegate next,
        IUnitOfWorkOperationManager operationManager, TaskCompletionSource<bool> completion)
    {
        IUnitOfWorkScope root = null;
        IUnitOfWorkExecutionScope participant = null;
        CancellationTokenSource callerTokenSource = null;
        var originalTokens = new List<(int Index, CancellationToken Token)>();
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;

        try
        {
            root = operationManager.Current;
            if (root is not IUnitOfWorkExecutionScope { IsActive: true })
                throw new InvalidOperationException("旧工作单元标记需要活动的业务操作 root 才能作为 participant 执行。");

            var callerToken = GetCallerToken(context.Parameters, out callerTokenSource);
            participant = operationManager.BeginOperation(cancellationToken: callerToken);
            ReplaceCancellationTokens(context.Parameters, participant.CancellationToken, originalTokens);
            var nextTask = next(context);
            if (nextTask == null)
                throw new InvalidOperationException("工作单元代理目标不能返回 null Task。");
            await nextTask.ConfigureAwait(false);
            RestoreCancellationTokens(context.Parameters, originalTokens);

            if (context.Implementation is ICommitAfter service && root.IsActive && !root.IsRollbackOnly)
                RegisterCommitAfter(root, service);

            await participant.CompleteAsync(participant.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        finally
        {
            RestoreCancellationTokens(context.Parameters, originalTokens);

            if (participant != null)
            {
                if (primaryException != null && participant.IsActive)
                {
                    try
                    {
                        await participant.RollbackAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        cleanupExceptions.Add(exception);
                    }
                }

                try
                {
                    await participant.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupExceptions.Add(exception);
                }
            }

            callerTokenSource?.Dispose();
        }

        if (primaryException != null)
        {
            AttachCleanupExceptions(primaryException, cleanupExceptions);
            completion.TrySetException(primaryException);
        }
        else if (cleanupExceptions.Count == 1)
        {
            completion.TrySetException(cleanupExceptions[0]);
        }
        else if (cleanupExceptions.Count > 1)
        {
            completion.TrySetException(new AggregateException(cleanupExceptions));
        }
        else
        {
            completion.TrySetResult(true);
        }
    }

    internal static Task InvokeAutomatic(AspectContext context, AspectDelegate next)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = CompleteAutomaticInvocationAsync(context, next, completion);
        return completion.Task;
    }

    private static async Task CompleteAutomaticInvocationAsync(AspectContext context, AspectDelegate next,
        TaskCompletionSource<bool> completion)
    {
        IUnitOfWorkExecutionScope operationScope = null;
        CancellationTokenSource callerTokenSource = null;
        var originalTokens = new List<(int Index, CancellationToken Token)>();
        var cleanupExceptions = new List<Exception>();
        Exception primaryException = null;

        try
        {
            var services = context.ServiceProvider;
            var operationManager = services.GetRequiredService<IUnitOfWorkOperationManager>();
            var callerToken = GetCallerToken(context.Parameters, out callerTokenSource);
            var options = operationManager.Current == null
                ? services.GetRequiredService<IOptions<BingAutomaticUnitOfWorkOptions>>().Value.DefaultOptions
                : null;

            operationScope = operationManager.BeginOperation(options, callerToken);
            ReplaceCancellationTokens(context.Parameters, operationScope.CancellationToken, originalTokens);
            var nextTask = next(context);
            if (nextTask == null)
                throw new InvalidOperationException("工作单元代理目标不能返回 null Task。");
            await nextTask.ConfigureAwait(false);
            RestoreCancellationTokens(context.Parameters, originalTokens);

            if (context.Implementation is ICommitAfter service && operationManager.Current is { } root &&
                root.IsActive && !root.IsRollbackOnly)
                RegisterCommitAfter(root, service);

            await operationScope.CompleteAsync(operationScope.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }
        finally
        {
            RestoreCancellationTokens(context.Parameters, originalTokens);

            if (operationScope != null)
            {
                if (primaryException != null && operationScope.IsActive)
                {
                    try
                    {
                        await operationScope.RollbackAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        cleanupExceptions.Add(exception);
                    }
                }

                try
                {
                    await operationScope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupExceptions.Add(exception);
                }
            }

            callerTokenSource?.Dispose();
        }

        if (primaryException != null)
        {
            AttachCleanupExceptions(primaryException, cleanupExceptions);
            completion.TrySetException(primaryException);
        }
        else if (cleanupExceptions.Count == 1)
        {
            completion.TrySetException(cleanupExceptions[0]);
        }
        else if (cleanupExceptions.Count > 1)
        {
            completion.TrySetException(new AggregateException(cleanupExceptions));
        }
        else
        {
            completion.TrySetResult(true);
        }
    }

    private static CancellationToken GetCallerToken(object[] parameters,
        out CancellationTokenSource linkedTokenSource)
    {
        linkedTokenSource = null;
        var tokens = (parameters ?? Array.Empty<object>())
            .OfType<CancellationToken>()
            .Distinct()
            .ToArray();
        if (tokens.Length == 0)
            return CancellationToken.None;
        if (tokens.Length == 1)
            return tokens[0];

        linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokens);
        return linkedTokenSource.Token;
    }

    private static void ReplaceCancellationTokens(object[] parameters, CancellationToken operationToken,
        ICollection<(int Index, CancellationToken Token)> originalTokens)
    {
        if (parameters == null)
            return;
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index] is not CancellationToken token)
                continue;
            originalTokens.Add((index, token));
            parameters[index] = operationToken;
        }
    }

    private static void RestoreCancellationTokens(object[] parameters,
        IList<(int Index, CancellationToken Token)> originalTokens)
    {
        if (parameters == null)
            return;
        for (var index = originalTokens.Count - 1; index >= 0; index--)
        {
            var original = originalTokens[index];
            if (original.Index < parameters.Length)
                parameters[original.Index] = original.Token;
        }
        originalTokens.Clear();
    }

    private static void RegisterCommitAfter(IUnitOfWorkScope root, ICommitAfter service)
    {
        var registrations = CommitAfterServices.GetValue(root, static _ => new CommitAfterServiceSet());
        lock (registrations)
        {
            if (!registrations.Services.Add(service))
                return;
            try
            {
                root.OnCompleted(() =>
                {
                    service.CommitAfter();
                    return Task.CompletedTask;
                });
            }
            catch
            {
                registrations.Services.Remove(service);
                throw;
            }
        }
    }

    private static void AttachCleanupExceptions(Exception primaryException,
        IReadOnlyCollection<Exception> cleanupExceptions)
    {
        if (cleanupExceptions.Count == 0)
            return;
        try
        {
            primaryException.Data["Bing.Operation.CleanupExceptions"] = cleanupExceptions.ToArray();
        }
        catch
        {
            // 清理诊断不能替换业务异常。
        }
    }

    private sealed class CommitAfterServiceSet
    {
        public HashSet<object> Services { get; } = new(CommitAfterReferenceComparer.Instance);
    }

    private sealed class CommitAfterReferenceComparer : IEqualityComparer<object>
    {
        public static CommitAfterReferenceComparer Instance { get; } = new();
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }

    /// <summary>
    /// 等待目标调用完成并执行工作单元提交及回调。
    /// </summary>
    /// <param name="context">当前拦截上下文。</param>
    /// <param name="nextTask">目标及后续拦截器调用。</param>
    private Task InvokeCoreAsync(AspectContext context, Task nextTask)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = CompleteInvocationAsync(context, nextTask, completion);
        return completion.Task;
    }

    /// <summary>
    /// 等待真实目标与提交结果，并将捕获的异常交给可观察的完成任务。
    /// </summary>
    /// <param name="context">当前拦截上下文。</param>
    /// <param name="nextTask">目标及后续拦截器调用。</param>
    /// <param name="completion">用于向 AspectCore 返回最终状态的任务源。</param>
    private static async Task CompleteInvocationAsync(AspectContext context, Task nextTask,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            await nextTask.ConfigureAwait(false);
            var manager = context.ServiceProvider.GetService<IUnitOfWorkManager>();
            if (manager == null)
            {
                completion.TrySetResult(true);
                return;
            }

            await manager.CommitAsync().ConfigureAwait(false);
            if (context.Implementation is ICommitAfter service)
            {
                var scope = context.ServiceProvider.GetService<IUnitOfWorkScopeManager>()?.Current;
                if (scope == null || scope.IsCompleted)
                {
                    service.CommitAfter();
                }
                else if (scope.IsActive && !scope.IsRollbackOnly)
                {
                    scope.OnCompleted(() =>
                    {
                        service.CommitAfter();
                        return Task.CompletedTask;
                    });
                }
            }

            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            // 将取消异常以 Faulted 状态传回，使 AspectCore 3.0.0 快路径继续传播原异常与令牌。
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// 将中立工作单元范围映射为 AspectCore 范围。
    /// </summary>
    /// <param name="scope">中立工作单元范围。</param>
    /// <returns>AspectCore 工作范围。</returns>
    private static Scope MapScope(BingUnitOfWorkScope scope)
    {
        return scope switch
        {
            BingUnitOfWorkScope.None => Scope.None,
            BingUnitOfWorkScope.Nested => Scope.Nested,
            _ => Scope.Aspect
        };
    }

}

/// <summary>
/// 为自动模式中的每个标记调用建立 owner 或 participant，不受旧 Aspect scope 抑制。
/// </summary>
internal sealed class BingAutomaticUnitOfWorkInterceptor : AbstractInterceptorAttribute
{
    public BingAutomaticUnitOfWorkInterceptor(UnitOfWorkAttribute attribute)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));
        Order = attribute.Order;
        Inherited = true;
    }

    /// <inheritdoc />
    public override Task Invoke(AspectContext context, AspectDelegate next)
    {
        if (context.ServiceProvider.GetService<BingAutomaticUnitOfWorkRegistrationMarker>() == null)
            return next(context);

        var adaptedContext = BingUnitOfWorkValueTaskInterceptor.AdaptContext(context);
        return BingUnitOfWorkInterceptor.InvokeAutomatic(adaptedContext, next);
    }
}

/// <summary>
/// 在工作单元标记的 ValueTask 调用中缓存单次消费结果，且不参与 Aspect scope 抑制。
/// </summary>
internal sealed class BingUnitOfWorkValueTaskInterceptor : AbstractInterceptorAttribute
{
    /// <summary>
    /// 初始化工作单元 ValueTask 结果适配器。
    /// </summary>
    /// <param name="attribute">工作单元标记。</param>
    public BingUnitOfWorkValueTaskInterceptor(UnitOfWorkAttribute attribute)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));

        Order = attribute.Order;
        Inherited = true;
    }

    /// <inheritdoc />
    public override Task Invoke(AspectContext context, AspectDelegate next)
    {
        var adaptedContext = AdaptContext(context);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _ = CompleteInvocationAsync(next(adaptedContext), completion);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        return completion.Task;
    }

    internal static AspectContext AdaptContext(AspectContext context) =>
        new UnitOfWorkValueTaskAspectContext(context);

    private static async Task CompleteInvocationAsync(Task nextTask, TaskCompletionSource<bool> completion)
    {
        try
        {
            await nextTask.ConfigureAwait(false);
            completion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            // scope 会抑制内层提交拦截器，因此取消必须由独立结果适配器继续作为故障传播。
            completion.TrySetException(exception);
        }
    }

    /// <summary>
    /// 转接 AspectContext，并把 ValueTask 的单次结果缓存为 Task-backed ValueTask。
    /// </summary>
    private sealed class UnitOfWorkValueTaskAspectContext : AspectContext
    {
        private static readonly ConcurrentDictionary<MethodInfo, MethodReflector> Reflectors = new();
        private readonly AspectContext _context;

        public UnitOfWorkValueTaskAspectContext(AspectContext context) => _context = context;

        public override IDictionary<string, object> AdditionalData => _context.AdditionalData;

        public override object ReturnValue
        {
            get => _context.ReturnValue;
            set => _context.ReturnValue = value;
        }

        public override IServiceProvider ServiceProvider => _context.ServiceProvider;

        public override MethodInfo ServiceMethod => _context.ServiceMethod;

        public override object Implementation => _context.Implementation;

        public override MethodInfo ImplementationMethod => _context.ImplementationMethod;

        public override object[] Parameters => _context.Parameters;

        public override MethodInfo ProxyMethod => _context.ProxyMethod;

        public override MethodInfo PredicateMethod => _context.PredicateMethod;

        public override object Proxy => _context.Proxy;

        public override Task Break() => _context.Break();

        public override Task Invoke(AspectDelegate next) => next(this);

        public override Task Complete()
        {
            var implementationMethod = ImplementationMethod;
            if (Implementation == null || implementationMethod == null ||
                !IsValueTask(implementationMethod.ReturnType))
                return _context.Complete();

            return CompleteValueTaskAsync(implementationMethod);
        }

        private Task CompleteValueTaskAsync(MethodInfo implementationMethod)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = CompleteValueTaskCoreAsync(implementationMethod, completion);
            return completion.Task;
        }

        private async Task CompleteValueTaskCoreAsync(MethodInfo implementationMethod,
            TaskCompletionSource<bool> completion)
        {
            try
            {
                // 使用 AspectCore 的非虚 call 反射器执行真实实现，避免类代理重新进入覆写方法。
                var reflector = Reflectors.GetOrAdd(implementationMethod,
                    static method => method.GetReflector(
                        RequiresVirtualDispatch(method) ? CallOptions.Callvirt : CallOptions.Call));
                var returnValue = reflector.Invoke(Implementation, Parameters);

                if (returnValue is ValueTask valueTask)
                {
                    var task = valueTask.AsTask();
                    ReturnValue = new ValueTask(task);
                    await task.ConfigureAwait(false);
                    completion.TrySetResult(true);
                    return;
                }

                var returnType = implementationMethod.ReturnType;
                var asTaskMethod = returnType.GetMethod(nameof(ValueTask<int>.AsTask), Type.EmptyTypes)
                    ?? throw new InvalidOperationException($"无法为 {returnType} 创建可重复读取的 ValueTask 结果。");
                Task genericTask;
                try
                {
                    genericTask = (Task)asTaskMethod.Invoke(returnValue, null)!;
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }

                ReturnValue = Activator.CreateInstance(returnType, new object[] { genericTask })
                    ?? throw new InvalidOperationException($"无法为 {returnType} 保存 ValueTask 结果。");
                await genericTask.ConfigureAwait(false);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private static bool IsValueTask(Type returnType) =>
            returnType == typeof(ValueTask) ||
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>);

        private static bool RequiresVirtualDispatch(MethodInfo method) =>
            method.IsPrivate && method.IsVirtual && method.IsFinal ||
            method.DeclaringType?.IsClass != true;
    }
}
