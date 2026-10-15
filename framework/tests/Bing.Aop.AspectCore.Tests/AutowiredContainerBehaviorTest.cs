using AspectCore.Extensions.Hosting;
using AspectCore.DependencyInjection;
using AspectCore.DynamicProxy;
using AspectCore.Extensions.AspectScope;
using Bing.Aspects;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Bing.Aop.AspectCore;

/// <summary>
/// 使用真实 AspectCore 服务容器验证属性和字段注入行为。
/// </summary>
public class AutowiredContainerBehaviorTest
{
    /// <summary>
    /// 验证属性注入以及字段未注入的当前行为。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectAutowiredPropertyButNotField()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<PropertyAndFieldConsumer>();
        });

        var service = host.Services.GetRequiredService<InjectedService>();
        var consumer = host.Services.GetRequiredService<PropertyAndFieldConsumer>();

        consumer.Service.ShouldBeSameAs(service);
        consumer.FieldService.ShouldBeNull();
    }

    /// <summary>
    /// 验证仅启用 AspectCore 服务上下文时不会隐式启用中立属性注入。
    /// </summary>
    [Fact]
    public void Resolve_WhenOnlyServiceContextIsEnabled_ShouldNotInjectNeutralAttribute()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddSingleton<InjectedService>();
                services.AddTransient<PropertyAndFieldConsumer>();
            })
            .Build();

        host.Services.GetRequiredService<PropertyAndFieldConsumer>().Service.ShouldBeNull();
    }

    /// <summary>
    /// 验证独立入口注册 Bing 属性注入工厂。
    /// </summary>
    [Fact]
    public void EnableAutowired_ShouldRegisterBingPropertyInjectorFactory()
    {
        using var host = CreateHost(_ => { });

        var factory = host.Services.GetRequiredService<global::AspectCore.DependencyInjection.IPropertyInjectorFactory>();

        factory.GetType().Name.ShouldBe("BingPropertyInjectorFactory");
        host.Services.GetServices<global::AspectCore.DependencyInjection.IServiceResolveCallback>()
            .ShouldContain(callback => callback.GetType().Name == "BingPropertyInjectorCallback");
    }

    /// <summary>
    /// 验证重复启用属性注入不会重复注册适配器。
    /// </summary>
    [Fact]
    public void EnableAutowired_WhenCalledRepeatedly_ShouldRegisterAdapterOnce()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAutowired();
                services.EnableAutowired();
            })
            .Build();

        host.Services.GetServices<global::AspectCore.DependencyInjection.IServiceResolveCallback>()
            .Count(callback => callback.GetType().Name == "BingPropertyInjectorCallback")
            .ShouldBe(1);
    }

    /// <summary>
    /// 验证预先注册的自定义属性注入工厂不会被覆盖。
    /// </summary>
    [Fact]
    public void EnableAutowired_ShouldRespectCustomPropertyInjectorFactory()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.AddScoped<global::AspectCore.DependencyInjection.IPropertyInjectorFactory,
                    CustomPropertyInjectorFactory>();
                services.EnableAutowired();
            })
            .Build();

        host.Services.GetRequiredService<global::AspectCore.DependencyInjection.IPropertyInjectorFactory>()
            .ShouldBeOfType<CustomPropertyInjectorFactory>();
    }

    /// <summary>
    /// 验证注册的工厂能够直接创建并执行属性注入器。
    /// </summary>
    [Fact]
    public void EnableAutowired_ShouldCreateInjectorForAutowiredProperty()
    {
        using var host = CreateHost(services => services.AddSingleton<InjectedService>());

        var factory = host.Services.GetRequiredService<global::AspectCore.DependencyInjection.IPropertyInjectorFactory>();
        var consumer = new PropertyAndFieldConsumer();
        factory.Create(typeof(PropertyAndFieldConsumer)).Invoke(consumer);

        consumer.Service.ShouldBeSameAs(host.Services.GetRequiredService<InjectedService>());
    }

    /// <summary>
    /// 验证启用 AOP 时会自动启用中立属性注入。
    /// </summary>
    [Fact]
    public void EnableAop_ShouldEnableAutowiredAdapter()
    {
        using var host = new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAop();
                services.AddSingleton<InjectedService>();
                services.AddTransient<PropertyAndFieldConsumer>();
            })
            .Build();

        host.Services.GetRequiredService<PropertyAndFieldConsumer>().Service.ShouldNotBeNull();
    }

    /// <summary>
    /// 验证重复启用 AOP 不会重复注册作用域服务。
    /// </summary>
    [Fact]
    public void EnableAop_WhenCalledRepeatedly_ShouldRegisterAspectServicesOnce()
    {
        var services = new ServiceCollection();

        services.EnableAop();
        services.EnableAop();

        services.Count(descriptor => descriptor.ServiceType == typeof(IInterceptorCollector)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IAspectScheduler)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IAspectBuilderFactory)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IAspectContextFactory)).ShouldBe(1);
    }

    /// <summary>
    /// 验证原生 FromServiceContext 特性仍可注入。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectNativeFromServiceContextProperty()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<NativePropertyConsumer>();
        });

        var consumer = host.Services.GetRequiredService<NativePropertyConsumer>();

        consumer.Service.ShouldBeSameAs(host.Services.GetRequiredService<InjectedService>());
    }

    /// <summary>
    /// 验证不同属性分别使用两种标记时各只注入一次。
    /// </summary>
    [Fact]
    public void Resolve_WhenDifferentPropertiesUseBothMarkers_ShouldSetEachOnce()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<MixedMarkedConsumer>();
        });

        var consumer = host.Services.GetRequiredService<MixedMarkedConsumer>();

        consumer.BingSetCount.ShouldBe(1);
        consumer.NativeSetCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证继承的原生 FromServiceContext 特性仍可注入。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectInheritedNativeFromServiceContextProperty()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<DerivedNativePropertyConsumer>();
        });

        var consumer = host.Services.GetRequiredService<DerivedNativePropertyConsumer>();

        consumer.Service.ShouldBeSameAs(host.Services.GetRequiredService<InjectedService>());
    }

    /// <summary>
    /// 验证继承的原生标记属性只执行一次注入。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectInheritedNativePropertyOnce()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<DerivedNativePropertyConsumer>();
        });

        var consumer = host.Services.GetRequiredService<DerivedNativePropertyConsumer>();

        consumer.SetCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证同一属性同时使用两个注入标记时只设置一次。
    /// </summary>
    [Fact]
    public void Resolve_WhenPropertyHasBothMarkers_ShouldSetItOnce()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<DoubleMarkedConsumer>();
        });

        var consumer = host.Services.GetRequiredService<DoubleMarkedConsumer>();

        consumer.SetCount.ShouldBe(1);
        consumer.Service.ShouldBeSameAs(host.Services.GetRequiredService<InjectedService>());
    }

    /// <summary>
    /// 验证属性注入使用当前作用域解析 Scoped 服务。
    /// </summary>
    [Fact]
    public void ResolveInDifferentScopes_ShouldUseDifferentScopedPropertyServices()
    {
        using var host = CreateHost(services =>
        {
            services.AddScoped<ScopedInjectedService>();
            services.AddTransient<ScopedPropertyConsumer>();
        });

        using var firstScope = host.Services.CreateScope();
        using var secondScope = host.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ScopedPropertyConsumer>();
        var second = secondScope.ServiceProvider.GetRequiredService<ScopedPropertyConsumer>();

        first.Service.ShouldNotBeSameAs(second.Service);
    }

    /// <summary>
    /// 验证继承属性可被注入。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectAutowiredMemberDeclaredOnBaseClass()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<DerivedConsumer>();
        });

        var consumer = host.Services.GetRequiredService<DerivedConsumer>();

        consumer.Service.ShouldNotBeNull();
    }

    /// <summary>
    /// 验证中间基类的属性标记可沿重写链传递到末级类型。
    /// </summary>
    [Fact]
    public void Resolve_ShouldInjectAutowiredMarkerDeclaredOnIntermediateOverride()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<LeafConsumer>();
        });

        var consumer = host.Services.GetRequiredService<LeafConsumer>();

        consumer.Service.ShouldNotBeNull();
    }

    /// <summary>
    /// 验证缺失服务不会自动导致属性注入失败。
    /// </summary>
    [Fact]
    public void Resolve_WhenAutowiredServiceIsMissing_ShouldLeavePropertyNull()
    {
        using var host = CreateHost(services => services.AddTransient<MissingServiceConsumer>());

        var consumer = host.Services.GetRequiredService<MissingServiceConsumer>();

        consumer.Service.ShouldBeNull();
    }

    /// <summary>
    /// 验证缺失服务时保留属性已有值。
    /// </summary>
    [Fact]
    public void Resolve_WhenAutowiredServiceIsMissing_ShouldPreserveExistingValue()
    {
        using var host = CreateHost(services => services.AddTransient<PrepopulatedConsumer>());

        var consumer = host.Services.GetRequiredService<PrepopulatedConsumer>();

        consumer.Service.ShouldBeSameAs(consumer.InitialValue);
    }

    /// <summary>
    /// 验证属性 setter 异常会传播。
    /// </summary>
    [Fact]
    public void Resolve_WhenAutowiredSetterThrows_ShouldPropagateSetterException()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<ThrowingSetterConsumer>();
        });

        Should.Throw<SetterFailureException>(() => host.Services.GetRequiredService<ThrowingSetterConsumer>());
    }

    /// <summary>
    /// 验证索引器和非公开 setter 不参与属性注入。
    /// </summary>
    [Fact]
    public void Resolve_ShouldIgnoreIndexerAndNonPublicSetter()
    {
        using var host = CreateHost(services =>
        {
            services.AddSingleton<InjectedService>();
            services.AddTransient<UnsupportedPropertyConsumer>();
        });

        var consumer = host.Services.GetRequiredService<UnsupportedPropertyConsumer>();

        consumer.IndexedService.ShouldBeNull();
        consumer.PrivateSetterService.ShouldBeNull();
    }

    /// <summary>
    /// 验证 AspectCore 宿主释放其拥有的服务。
    /// </summary>
    [Fact]
    public void DisposeHost_ShouldDisposeInjectedSingleton()
    {
        var disposable = new DisposableInjectedService();
        var host = CreateHost(services =>
        {
            services.AddSingleton(disposable);
            services.AddTransient<DisposableConsumer>();
        });

        host.Services.GetRequiredService<DisposableConsumer>().Service.ShouldBeSameAs(disposable);
        host.Dispose();

        disposable.IsDisposed.ShouldBeTrue();
    }

    /// <summary>
    /// 验证属性注入的作用域服务在作用域释放时只释放一次。
    /// </summary>
    [Fact]
    public void DisposeScope_ShouldDisposeScopedAutowiredServiceOnce()
    {
        using var host = CreateHost(services =>
        {
            services.AddScoped<ScopedDisposableInjectedService>();
            services.AddTransient<ScopedDisposableConsumer>();
        });

        ScopedDisposableInjectedService service;
        using (var scope = host.Services.CreateScope())
        {
            service = scope.ServiceProvider.GetRequiredService<ScopedDisposableConsumer>().Service;
            service.ShouldNotBeNull();
            service.DisposeCount.ShouldBe(0);
        }

        service.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证 AspectCore 作用域释放路径在当前版本使用同步释放接口一次。
    /// </summary>
    [Fact]
    public async Task DisposeScopeAsync_ShouldUseProviderSupportedReleasePath()
    {
        var host = CreateHost(services =>
        {
            services.AddScoped<AsyncDisposableInjectedService>();
            services.AddScoped<AsyncDisposableConsumer>();
        });

        AsyncDisposableInjectedService disposable;
        var scope = host.Services.CreateAsyncScope();
        disposable = scope.ServiceProvider.GetRequiredService<AsyncDisposableConsumer>().Service;
        await scope.DisposeAsync();
        host.Dispose();

        disposable.AsyncDisposeCount.ShouldBe(0);
        disposable.SyncDisposeCount.ShouldBe(1);
    }

    /// <summary>
    /// 验证作用域工厂释放后不会继续持有可回收程序集中的类型。
    /// </summary>
    [Fact]
    public void DisposeScope_ShouldReleaseCollectiblePropertyDescriptorCache()
    {
        var collectibleType = CreateCollectibleAssemblyAndPopulatePropertyCache();

        for (var attempt = 0; attempt < 16 && collectibleType.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetTotalMemory(forceFullCollection: true);
        }

        collectibleType.IsAlive.ShouldBeFalse();
    }

    /// <summary>
    /// 在独立方法中创建可回收类型，避免测试方法本地变量延长引用生命周期。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateCollectibleAssemblyAndPopulatePropertyCache()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("BingAspectCoreCollectiblePropertyAssembly"),
            AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Main");
        var typeBuilder = module.DefineType(
            "CollectiblePropertyConsumer",
            TypeAttributes.Public | TypeAttributes.Class);
        var propertyBuilder = typeBuilder.DefineProperty(
            "Service",
            PropertyAttributes.None,
            typeof(InjectedService),
            Type.EmptyTypes);
        var getter = typeBuilder.DefineMethod(
            "get_Service",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(InjectedService),
            Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldnull);
        getterIl.Emit(OpCodes.Ret);
        var setter = typeBuilder.DefineMethod(
            "set_Service",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(void),
            new[] { typeof(InjectedService) });
        setter.GetILGenerator().Emit(OpCodes.Ret);
        propertyBuilder.SetGetMethod(getter);
        propertyBuilder.SetSetMethod(setter);
        propertyBuilder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(AutowiredAttribute).GetConstructor(Type.EmptyTypes),
            Array.Empty<object>()));
        var collectibleType = typeBuilder.CreateType();
        var typeReference = new WeakReference(collectibleType);

        using (var host = CreateHost(_ => { }))
        using (var scope = host.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<global::AspectCore.DependencyInjection.IPropertyInjectorFactory>();
            factory.Create(collectibleType);
        }

        collectibleType = null;
        typeBuilder = null;
        module = null;
        assembly = null;
        return typeReference;
    }

    /// <summary>
    /// 创建启用 AspectCore 服务上下文的宿主。
    /// </summary>
    private static IHost CreateHost(Action<IServiceCollection> configureServices)
    {
        return new HostBuilder()
            .UseServiceContext()
            .ConfigureServices(services =>
            {
                services.EnableAutowired();
                configureServices(services);
            })
            .Build();
    }

    /// <summary>
    /// 被注入的服务。
    /// </summary>
    public sealed class InjectedService;

    /// <summary>
    /// 同时声明注入属性与字段的消费者。
    /// </summary>
    public sealed class PropertyAndFieldConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        public InjectedService Service { get; set; }

        /// <summary>
        /// 当前不会被 AspectCore 注入的字段。
        /// </summary>
        [Autowired]
        public InjectedService FieldService;
    }

    /// <summary>
    /// 使用 AspectCore 原生属性标记的消费者。
    /// </summary>
    public sealed class NativePropertyConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [global::AspectCore.DependencyInjection.FromServiceContext]
        public InjectedService Service { get; set; }
    }

    /// <summary>
    /// 分别声明两种注入标记的消费者。
    /// </summary>
    public sealed class MixedMarkedConsumer
    {
        /// <summary>
        /// 缓存 Bing 标记属性值。
        /// </summary>
        private InjectedService _bingService;

        /// <summary>
        /// 缓存原生标记属性值。
        /// </summary>
        private InjectedService _nativeService;

        /// <summary>
        /// 获取或设置 Bing 标记服务。
        /// </summary>
        [Autowired]
        public InjectedService BingService
        {
            get => _bingService;
            set
            {
                BingSetCount++;
                _bingService = value;
            }
        }

        /// <summary>
        /// 获取或设置原生标记服务。
        /// </summary>
        [global::AspectCore.DependencyInjection.FromServiceContext]
        public InjectedService NativeService
        {
            get => _nativeService;
            set
            {
                NativeSetCount++;
                _nativeService = value;
            }
        }

        /// <summary>
        /// 获取 Bing 标记 setter 调用次数。
        /// </summary>
        public int BingSetCount { get; private set; }

        /// <summary>
        /// 获取原生标记 setter 调用次数。
        /// </summary>
        public int NativeSetCount { get; private set; }
    }

    /// <summary>
    /// 声明继承原生注入标记的基类消费者。
    /// </summary>
    public class NativeBasePropertyConsumer
    {
        /// <summary>
        /// 缓存注入的服务。
        /// </summary>
        private InjectedService _service;

        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [global::AspectCore.DependencyInjection.FromServiceContext]
        public virtual InjectedService Service
        {
            get => _service;
            set
            {
                SetCount++;
                _service = value;
            }
        }

        /// <summary>
        /// 获取 setter 调用次数。
        /// </summary>
        public int SetCount { get; private set; }
    }

    /// <summary>
    /// 继承原生注入标记的消费者。
    /// </summary>
    public sealed class DerivedNativePropertyConsumer : NativeBasePropertyConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        public override InjectedService Service
        {
            get => base.Service;
            set => base.Service = value;
        }
    }

    /// <summary>
    /// 同时使用 Bing 和 AspectCore 属性标记的消费者。
    /// </summary>
    public sealed class DoubleMarkedConsumer
    {
        /// <summary>
        /// 缓存注入的服务。
        /// </summary>
        private InjectedService _service;

        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        [global::AspectCore.DependencyInjection.FromServiceContext]
        public InjectedService Service
        {
            get => _service;
            set
            {
                SetCount++;
                _service = value;
            }
        }

        /// <summary>
        /// 获取 setter 调用次数。
        /// </summary>
        public int SetCount { get; private set; }
    }

    /// <summary>
    /// 作用域服务。
    /// </summary>
    public sealed class ScopedInjectedService;

    /// <summary>
    /// 接收作用域属性服务的消费者。
    /// </summary>
    public sealed class ScopedPropertyConsumer
    {
        /// <summary>
        /// 获取或设置作用域服务。
        /// </summary>
        [Autowired]
        public ScopedInjectedService Service { get; set; }
    }

    /// <summary>
    /// 作用域可释放服务。
    /// </summary>
    public sealed class ScopedDisposableInjectedService : IDisposable
    {
        /// <summary>
        /// 获取释放次数。
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => DisposeCount++;
    }

    /// <summary>
    /// 接收作用域可释放属性服务的消费者。
    /// </summary>
    public sealed class ScopedDisposableConsumer
    {
        /// <summary>
        /// 获取或设置作用域可释放服务。
        /// </summary>
        [Autowired]
        public ScopedDisposableInjectedService Service { get; set; }
    }

    /// <summary>
    /// 声明可继承注入属性的基类。
    /// </summary>
    public class BaseConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        public virtual InjectedService Service { get; set; }
    }

    /// <summary>
    /// 继承注入属性的消费者。
    /// </summary>
    public sealed class DerivedConsumer : BaseConsumer;

    /// <summary>
    /// 使用中间基类注入标记的基类。
    /// </summary>
    public class MiddleConsumer : BaseConsumer
    {
        /// <summary>
        /// 获取或设置中间基类声明的服务。
        /// </summary>
        [Autowired]
        public override InjectedService Service { get; set; }
    }

    /// <summary>
    /// 再次重写中间基类属性的消费者。
    /// </summary>
    public sealed class LeafConsumer : MiddleConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        public override InjectedService Service { get; set; }
    }

    /// <summary>
    /// 依赖缺失服务的消费者。
    /// </summary>
    public sealed class MissingServiceConsumer
    {
        /// <summary>
        /// 获取或设置缺失的服务。
        /// </summary>
        [Autowired]
        public MissingService Service { get; set; }
    }

    /// <summary>
    /// 未注册的服务。
    /// </summary>
    public sealed class MissingService;

    /// <summary>
    /// 具有属性初始值的消费者。
    /// </summary>
    public sealed class PrepopulatedConsumer
    {
        /// <summary>
        /// 初始化一个 <see cref="PrepopulatedConsumer" /> 类型的实例。
        /// </summary>
        public PrepopulatedConsumer()
        {
            InitialValue = new InjectedService();
            Service = InitialValue;
        }

        /// <summary>
        /// 获取构造期间设置的初始值。
        /// </summary>
        public InjectedService InitialValue { get; }

        /// <summary>
        /// 获取或设置可选注入服务。
        /// </summary>
        [Autowired]
        public InjectedService Service { get; set; }
    }

    /// <summary>
    /// 通过 setter 抛出异常的消费者。
    /// </summary>
    public sealed class ThrowingSetterConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        public InjectedService Service
        {
            get => null;
            set => throw new SetterFailureException();
        }
    }

    /// <summary>
    /// 属性 setter 异常。
    /// </summary>
    public sealed class SetterFailureException : Exception;

    /// <summary>
    /// 声明不支持注入成员的消费者。
    /// </summary>
    public sealed class UnsupportedPropertyConsumer
    {
        /// <summary>
        /// 获取索引器值。
        /// </summary>
        [Autowired]
        public InjectedService this[int index]
        {
            get => null;
            set { }
        }

        /// <summary>
        /// 获取或设置使用非公开 setter 的服务。
        /// </summary>
        [Autowired]
        public InjectedService PrivateSetterService { get; private set; }

        /// <summary>
        /// 获取索引器服务。
        /// </summary>
        public InjectedService IndexedService => this[0];
    }

    /// <summary>
    /// 可释放的注入服务。
    /// </summary>
    public sealed class DisposableInjectedService : IDisposable
    {
        /// <summary>
        /// 获取服务是否已释放。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc />
        public void Dispose() => IsDisposed = true;
    }

    /// <summary>
    /// 依赖可释放属性服务的消费者。
    /// </summary>
    public sealed class DisposableConsumer
    {
        /// <summary>
        /// 获取或设置注入的服务。
        /// </summary>
        [Autowired]
        public DisposableInjectedService Service { get; set; }
    }

    /// <summary>
    /// 同时实现同步和异步释放的注入服务。
    /// </summary>
    public sealed class AsyncDisposableInjectedService : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 获取同步释放次数。
        /// </summary>
        public int SyncDisposeCount { get; private set; }

        /// <summary>
        /// 获取异步释放次数。
        /// </summary>
        public int AsyncDisposeCount { get; private set; }

        /// <inheritdoc />
        public void Dispose() => SyncDisposeCount++;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            AsyncDisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 依赖异步释放服务的消费者。
    /// </summary>
    public sealed class AsyncDisposableConsumer
    {
        /// <summary>
        /// 获取或设置异步释放服务。
        /// </summary>
        [Autowired]
        public AsyncDisposableInjectedService Service { get; set; }
    }

    /// <summary>
    /// 用于验证自定义工厂优先级的属性注入工厂。
    /// </summary>
    private sealed class CustomPropertyInjectorFactory :
        global::AspectCore.DependencyInjection.IPropertyInjectorFactory
    {
        /// <inheritdoc />
        public global::AspectCore.DependencyInjection.IPropertyInjector Create(Type type)
        {
            return new NoopPropertyInjector();
        }
    }

    /// <summary>
    /// 用于验证自定义工厂注册的空属性注入器。
    /// </summary>
    private sealed class NoopPropertyInjector : global::AspectCore.DependencyInjection.IPropertyInjector
    {
        /// <inheritdoc />
        public void Invoke(object instance)
        {
        }
    }
}
