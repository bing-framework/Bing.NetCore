using System.Reflection;
using System.Reflection.Emit;
using Bing.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Tests.Modularity;

/// <summary>
/// 模块依赖图构建、校验和拓扑排序测试。
/// </summary>
public class ModuleGraphTest
{
    /// <summary>
    /// 验证菱形依赖按拓扑顺序加载。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldPreserveTopologicalOrderForDiamondGraph()
    {
        var modules = new BingModuleLoader().LoadModules(new[] { typeof(DiamondRoot) }, Array.Empty<Type>());

        modules.Select(x => x.ModuleType).ShouldBe(new[]
        {
            typeof(DiamondLeaf), typeof(DiamondLeft), typeof(DiamondRight), typeof(DiamondRoot)
        });
        modules.Single(x => x.ModuleType == typeof(DiamondLeft)).Dependencies.ShouldBe(new[] { typeof(DiamondLeaf) });
        modules.Single(x => x.ModuleType == typeof(DiamondRight)).Dependencies.ShouldBe(new[] { typeof(DiamondLeaf) });
    }

    /// <summary>
    /// 验证无依赖约束的就绪模块按类型全名稳定排序。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldUseOrdinalFullNameForReadyTie()
    {
        var modules = new BingModuleLoader().LoadModules(
            new[] { typeof(ReadyTieBeta), typeof(ReadyTieAlpha) }, Array.Empty<Type>());

        modules.Select(x => x.ModuleType).ShouldBe(new[]
        {
            typeof(ReadyTieAlpha), typeof(ReadyTieBeta)
        });
    }

    /// <summary>
    /// 验证多个根模块共享依赖时依赖只加载一次并先于两个根模块。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldDeduplicateSharedDependencyAcrossRoots()
    {
        var modules = new BingModuleLoader().LoadModules(
            new[] { typeof(SharedRootLeft), typeof(SharedRootRight) }, Array.Empty<Type>());
        var types = modules.Select(x => x.ModuleType).ToArray();

        types.Count(x => x == typeof(SharedRootDependency)).ShouldBe(1);
        Array.IndexOf(types, typeof(SharedRootDependency))
            .ShouldBeLessThan(Array.IndexOf(types, typeof(SharedRootLeft)));
        Array.IndexOf(types, typeof(SharedRootDependency))
            .ShouldBeLessThan(Array.IndexOf(types, typeof(SharedRootRight)));
    }

    /// <summary>
    /// 验证较长依赖链可以按拓扑顺序加载。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldHandleLongDependencyChainWithoutRecursion()
    {
        const int length = 128;
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Bing.Core.Tests.DynamicLongChain." + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var baseConstructor = typeof(BingModule).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null)!;
        var dependencyAttributeConstructor = typeof(LongChainDependencyAttribute).GetConstructor(new[] { typeof(int) })!;
        var expected = new List<Type>(length);
        Type previous = null;

        for (var index = 0; index < length; index++)
        {
            var typeBuilder = module.DefineType(
                "LongChain" + index.ToString("D3"),
                TypeAttributes.Public | TypeAttributes.Class,
                typeof(BingModule));
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);
            var il = constructor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseConstructor);
            il.Emit(OpCodes.Ret);

            if (previous != null)
                typeBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                    dependencyAttributeConstructor,
                    new object[] { index - 1 }));

            previous = typeBuilder.CreateTypeInfo()!.AsType();
            expected.Add(previous);
        }

        LongChainTypes = expected.ToArray();
        try
        {
            var loaded = new BingModuleLoader().LoadModules(new[] { previous }, Array.Empty<Type>());

            loaded.Count.ShouldBe(length);
            loaded.Select(x => x.ModuleType).ShouldBe(expected);
        }
        finally
        {
            LongChainTypes = Array.Empty<Type>();
        }
    }

    /// <summary>
    /// 验证自环依赖报告完整路径。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldRejectSelfCycleWithCompletePath()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(SelfCycle) }, Array.Empty<Type>()));

        AssertDependencyPath(exception, typeof(SelfCycle), typeof(SelfCycle));
        exception.Message.ShouldBe("Bing 模块依赖存在环: " + Path(typeof(SelfCycle), typeof(SelfCycle)));
    }

    /// <summary>
    /// 验证多节点环依赖报告完整路径。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldRejectThreeNodeCycleWithCompletePath()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(CycleA) }, Array.Empty<Type>()));

        AssertDependencyPath(exception, typeof(CycleA), typeof(CycleB), typeof(CycleC), typeof(CycleA));
        exception.Message.ShouldBe("Bing 模块依赖存在环: " + Path(typeof(CycleA), typeof(CycleB), typeof(CycleC), typeof(CycleA)));

        var rootedCycle = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(ExternalCycleRoot) }, Array.Empty<Type>()));
        AssertDependencyPath(rootedCycle, typeof(ExternalCycleRoot), typeof(CycleA), typeof(CycleB), typeof(CycleC), typeof(CycleA));
    }

    /// <summary>
    /// 验证排除必需依赖时报告冲突链。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldRejectExcludedDependencyWithConflictChain()
    {
        var exception = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(DiamondRoot) }, new[] { typeof(DiamondLeaf) }));

        AssertDependencyPath(exception, typeof(DiamondRoot), typeof(DiamondLeft), typeof(DiamondLeaf));
        exception.Message.ShouldBe("模块依赖链包含被排除的模块: " + Path(typeof(DiamondRoot), typeof(DiamondLeft), typeof(DiamondLeaf)));
    }

    /// <summary>
    /// 验证根模块参数和构造条件校验。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldValidateNullAndInvalidRoots()
    {
        Should.Throw<ArgumentNullException>(() =>
            new BingModuleLoader().LoadModules(null, Array.Empty<Type>()));
        var invalidRoot = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(string) }, Array.Empty<Type>()));
        AssertDependencyPath(invalidRoot, typeof(string));
        var genericRoot = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(GenericModule<>) }, Array.Empty<Type>()));
        AssertDependencyPath(genericRoot, typeof(GenericModule<>));
        var noConstructorRoot = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(NoPublicConstructorModule) }, Array.Empty<Type>()));
        AssertDependencyPath(noConstructorRoot, typeof(NoPublicConstructorModule));

        var invalidDependency = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(InvalidDependencyRoot) }, Array.Empty<Type>()));
        AssertDependencyPath(invalidDependency, typeof(InvalidDependencyRoot), typeof(InvalidDependencyMiddle), typeof(string));
        invalidDependency.Message.ShouldBe(new ArgumentException(
            "给定类型不是有效的 BingModule: " + Path(typeof(InvalidDependencyRoot), typeof(InvalidDependencyMiddle), typeof(string)),
            "dependency").Message);

        var noConstructorDependency = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(NoConstructorDependencyRoot) }, Array.Empty<Type>()));
        AssertDependencyPath(noConstructorDependency, typeof(NoConstructorDependencyRoot), typeof(NoConstructorDependencyMiddle), typeof(NoPublicConstructorModule));
    }

    /// <summary>
    /// 验证依赖提供程序返回空值或抛出异常时的路径信息。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldRejectNullDependencyAndProviderResult()
    {
        var nullDependency = Should.Throw<ArgumentException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(NullDependencyPathRoot) }, Array.Empty<Type>()));
        AssertDependencyPath(nullDependency, typeof(NullDependencyPathRoot), typeof(NullDependencyModule), null);
        var nullProvider = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(NullProviderPathRoot) }, Array.Empty<Type>()));
        AssertDependencyPath(nullProvider, typeof(NullProviderPathRoot), typeof(NullProviderResultModule));

        var providerError = Should.Throw<InvalidOperationException>(() =>
            new BingModuleLoader().LoadModules(new[] { typeof(ProviderPathRoot) }, Array.Empty<Type>()));
        providerError.ShouldBeSameAs(ProviderFailure);
        AssertDependencyPath(providerError, typeof(ProviderPathRoot), typeof(ThrowingProviderModule));
    }

    /// <summary>
    /// 验证自定义依赖提供程序和继承依赖的解析。
    /// </summary>
    [Fact]
    public void LoadModules_ShouldReadCustomProviderAndInheritedDependency()
    {
        var custom = new BingModuleLoader().LoadModules(new[] { typeof(CustomProviderModule) }, Array.Empty<Type>());
        custom.Select(x => x.ModuleType).ShouldBe(new[] { typeof(DiamondLeaf), typeof(CustomProviderModule) });

        var inherited = new BingModuleLoader().LoadModules(new[] { typeof(InheritedDependencyModule) }, Array.Empty<Type>());
        inherited.Select(x => x.ModuleType).ShouldBe(new[] { typeof(DiamondLeaf), typeof(InheritedDependencyModule) });
    }

    /// <summary>
    /// 验证公开模块判断与运行时加载契约保持一致。
    /// </summary>
    [Fact]
    public void IsBingModule_ShouldMatchRuntimeLoadableContract()
    {
        BingModule.IsBingModule(typeof(DiamondLeaf)).ShouldBeTrue();
        BingModule.IsBingModule(typeof(InterfaceOnlyModule)).ShouldBeFalse();
        BingModule.IsBingModule(typeof(GenericModule<>)).ShouldBeFalse();
        BingModule.IsBingModule(typeof(NoPublicConstructorModule)).ShouldBeFalse();
        BingModule.IsBingModule(typeof(AbstractModule)).ShouldBeFalse();
    }

    /// <summary>
    /// 断言异常包含预期的模块依赖路径。
    /// </summary>
    /// <param name="exception">待检查的异常。</param>
    /// <param name="expected">预期的依赖路径。</param>
    private static void AssertDependencyPath(Exception exception, params Type[] expected)
    {
        var path = exception.Data["Bing.ModuleDependencyPath"].ShouldBeOfType<Type[]>();
        path.ShouldBe(expected);
    }

    /// <summary>
    /// 格式化模块类型路径。
    /// </summary>
    /// <param name="types">模块类型序列。</param>
    /// <returns>以箭头连接的类型全名。</returns>
    private static string Path(params Type[] types) => string.Join(" -> ", types.Select(type => type.FullName));

    /// <summary>
    /// 动态长链测试的依赖类型提供程序。
    /// </summary>
    public sealed class LongChainDependencyAttribute : Attribute, IDependedTypesProvider
    {
        /// <summary>
        /// 依赖类型在测试链中的索引。
        /// </summary>
        private readonly int _dependencyIndex;

        /// <summary>
        /// 初始化长链依赖提供程序。
        /// </summary>
        /// <param name="dependencyIndex">依赖类型索引。</param>
        public LongChainDependencyAttribute(int dependencyIndex) => _dependencyIndex = dependencyIndex;

        /// <inheritdoc />
        public Type[] GetDependedTypes() => new[] { LongChainTypes[_dependencyIndex] };
    }

    /// <summary>
    /// 动态长链测试当前生成的模块类型。
    /// </summary>
    private static Type[] LongChainTypes = Array.Empty<Type>();

    /// <summary>
    /// 菱形依赖的左分支模块。
    /// </summary>
    [DependsOnModule(typeof(DiamondLeaf))]
    public class DiamondLeft : BingModule { }

    /// <summary>
    /// 菱形依赖的右分支模块。
    /// </summary>
    [DependsOnModule(typeof(DiamondLeaf))]
    public class DiamondRight : BingModule { }

    /// <summary>
    /// 菱形依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(DiamondLeft), typeof(DiamondRight))]
    public class DiamondRoot : BingModule { }

    /// <summary>
    /// 菱形依赖的叶子模块。
    /// </summary>
    public class DiamondLeaf : BingModule { }

    /// <summary>
    /// 就绪平局测试的后排序模块。
    /// </summary>
    public class ReadyTieBeta : BingModule { }

    /// <summary>
    /// 就绪平局测试的先排序模块。
    /// </summary>
    public class ReadyTieAlpha : BingModule { }

    /// <summary>
    /// 多根共享依赖测试的左根模块。
    /// </summary>
    [DependsOnModule(typeof(SharedRootDependency))]
    public class SharedRootLeft : BingModule { }

    /// <summary>
    /// 多根共享依赖测试的右根模块。
    /// </summary>
    [DependsOnModule(typeof(SharedRootDependency))]
    public class SharedRootRight : BingModule { }

    /// <summary>
    /// 多根共享依赖测试的共享模块。
    /// </summary>
    public class SharedRootDependency : BingModule { }

    /// <summary>
    /// 自环依赖模块。
    /// </summary>
    [DependsOnModule(typeof(SelfCycle))]
    public class SelfCycle : BingModule { }

    /// <summary>
    /// 多节点环依赖的第一个模块。
    /// </summary>
    [DependsOnModule(typeof(CycleB))]
    public class CycleA : BingModule { }

    /// <summary>
    /// 包含外部根节点的环依赖模块。
    /// </summary>
    [DependsOnModule(typeof(CycleA))]
    public class ExternalCycleRoot : BingModule { }

    /// <summary>
    /// 多节点环依赖的第二个模块。
    /// </summary>
    [DependsOnModule(typeof(CycleC))]
    public class CycleB : BingModule { }

    /// <summary>
    /// 多节点环依赖的第三个模块。
    /// </summary>
    [DependsOnModule(typeof(CycleA))]
    public class CycleC : BingModule { }

    /// <summary>
    /// 使用自定义依赖提供程序的模块。
    /// </summary>
    [ModuleDependencyProvider(typeof(DiamondLeaf))]
    public class CustomProviderModule : BingModule { }

    /// <summary>
    /// 提供继承依赖的基类模块。
    /// </summary>
    [DependsOnModule(typeof(DiamondLeaf))]
    public class InheritedDependencyBase : BingModule { }

    /// <summary>
    /// 继承依赖的具体模块。
    /// </summary>
    public class InheritedDependencyModule : InheritedDependencyBase { }

    /// <summary>
    /// 包含非法依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(InvalidDependencyMiddle))]
    public class InvalidDependencyRoot : BingModule { }

    /// <summary>
    /// 指向非法类型的依赖模块。
    /// </summary>
    [DependsOnModule(typeof(string))]
    public class InvalidDependencyMiddle : BingModule { }

    /// <summary>
    /// 包含无公共构造依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(NoConstructorDependencyMiddle))]
    public class NoConstructorDependencyRoot : BingModule { }

    /// <summary>
    /// 指向无公共构造模块的依赖模块。
    /// </summary>
    [DependsOnModule(typeof(NoPublicConstructorModule))]
    public class NoConstructorDependencyMiddle : BingModule { }

    /// <summary>
    /// 依赖提供程序返回空目录的模块。
    /// </summary>
    [NullProvider]
    public class NullProviderResultModule : BingModule { }

    /// <summary>
    /// 依赖提供程序返回空类型的模块。
    /// </summary>
    [NullProvider(emitNullType: true)]
    public class NullDependencyModule : BingModule { }

    /// <summary>
    /// 依赖提供程序抛出异常的模块。
    /// </summary>
    [ThrowingProvider]
    public class ThrowingProviderModule : BingModule { }

    /// <summary>
    /// 包含抛出异常依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(ThrowingProviderModule))]
    public class ProviderPathRoot : BingModule { }

    /// <summary>
    /// 包含空类型依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(NullDependencyModule))]
    public class NullDependencyPathRoot : BingModule { }

    /// <summary>
    /// 包含空目录依赖的根模块。
    /// </summary>
    [DependsOnModule(typeof(NullProviderResultModule))]
    public class NullProviderPathRoot : BingModule { }


    /// <summary>
    /// 用于验证泛型模块类型校验的模块。
    /// </summary>
    public class GenericModule<T> : BingModule { }

    /// <summary>
    /// 仅提供私有构造函数的模块。
    /// </summary>
    public class NoPublicConstructorModule : BingModule
    {
        /// <summary>
        /// 初始化不可公开构造的测试模块。
        /// </summary>
        private NoPublicConstructorModule() { }
    }

    /// <summary>
    /// 仅实现旧接口、未继承运行时基类的模块。
    /// </summary>
    private sealed class InterfaceOnlyModule : IBingModule
    {
        /// <inheritdoc />
        public ModuleLevel Level => ModuleLevel.Business;

        /// <inheritdoc />
        public int Order => 0;

        /// <inheritdoc />
        public bool Enabled => false;

        /// <inheritdoc />
        public IServiceCollection AddServices(IServiceCollection services) => services;

        /// <inheritdoc />
        public void UseModule(IServiceProvider provider) { }
    }

    /// <summary>
    /// 抽象模块类型。
    /// </summary>
    private abstract class AbstractModule : BingModule { }

    /// <summary>
    /// 返回单个固定依赖的测试特性。
    /// </summary>
    private sealed class ModuleDependencyProviderAttribute : Attribute, IDependedTypesProvider
    {
        /// <summary>
        /// 固定依赖类型。
        /// </summary>
        private readonly Type _dependency;

        /// <summary>
        /// 初始化依赖提供特性。
        /// </summary>
        /// <param name="dependency">依赖类型。</param>
        public ModuleDependencyProviderAttribute(Type dependency) => _dependency = dependency;

        /// <inheritdoc />
        public Type[] GetDependedTypes() => new[] { _dependency };
    }

    /// <summary>
    /// 可返回空依赖结果的测试特性。
    /// </summary>
    private sealed class NullProviderAttribute : Attribute, IDependedTypesProvider
    {
        /// <summary>
        /// 是否返回包含空类型的数组。
        /// </summary>
        private readonly bool _emitNullType;

        /// <summary>
        /// 初始化空结果提供特性。
        /// </summary>
        /// <param name="emitNullType">是否返回包含空类型的依赖数组。</param>
        public NullProviderAttribute(bool emitNullType = false) => _emitNullType = emitNullType;

        /// <inheritdoc />
        public Type[] GetDependedTypes() => _emitNullType ? new Type[] { null } : null;
    }

    /// <summary>
    /// 依赖提供程序测试异常。
    /// </summary>
    private static readonly InvalidOperationException ProviderFailure = new("provider failure");

    /// <summary>
    /// 始终抛出固定异常的测试特性。
    /// </summary>
    private sealed class ThrowingProviderAttribute : Attribute, IDependedTypesProvider
    {
        /// <inheritdoc />
        public Type[] GetDependedTypes() => throw ProviderFailure;
    }
}
