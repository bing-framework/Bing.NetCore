using Bing.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Bing.Tests.Reflections
{
    ///// <summary>
    ///// 类型查找器测试
    ///// </summary>
    //public class FinderTest:TestBase
    //{
    //    /// <summary>
    //    /// 类型查找器
    //    /// </summary>
    //    private readonly ITypeFinder _finder;

    //    /// <summary>
    //    /// 初始化一个<see cref="FinderTest"/>类型的实例
    //    /// </summary>
    //    public FinderTest(ITestOutputHelper output) : base(output)
    //    {
    //        _finder = new AppDomainTypeFinder();
    //    }

    //    /// <summary>
    //    /// 查找类型集合
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find()
    //    {
    //        var types = _finder.Find<IA>();
    //        Assert.Single(types);
    //        Assert.Equal(typeof(A), types[0]);
    //    }

    //    /// <summary>
    //    /// 查找类型集合
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find_2()
    //    {
    //        var types = _finder.Find<IB>();
    //        Assert.Equal(2, types.Count);
    //        Assert.Equal(typeof(A), types[0]);
    //        Assert.Equal(typeof(B), types[1]);
    //    }

    //    /// <summary>
    //    /// 查找的结果类型包含泛型
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find_3()
    //    {
    //        var types = _finder.Find<IC>();
    //        Assert.Equal(2, types.Count);
    //        Assert.Equal(typeof(B), types[0]);
    //        Assert.Equal(typeof(D<>), types[1]);
    //    }

    //    /// <summary>
    //    /// 查找泛型类型
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find_4()
    //    {
    //        var types = _finder.Find<IG<E>>();
    //        Assert.Single(types);
    //        Assert.Equal(typeof(E), types[0]);
    //    }

    //    /// <summary>
    //    /// 查找泛型类型
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find_5()
    //    {
    //        var types = _finder.Find(typeof(IG<>));
    //        Assert.Equal(2, types.Count);
    //        Assert.Equal(typeof(E), types[0]);
    //        Assert.Equal(typeof(F2<>), types[1]);
    //    }

    //    /// <summary>
    //    /// 测试并发
    //    /// </summary>
    //    [Fact]
    //    public void Test_Find_6()
    //    {
    //        Bing.Utils.Helpers.Thread.ParallelExecute(() => {
    //            var types = _finder.Find<IA>();
    //            Assert.Single(types);
    //            Assert.Equal(typeof(A), types[0]);
    //        }, () => {
    //            var types = _finder.Find<IB>();
    //            Assert.Equal(2, types.Count);
    //            Assert.Equal(typeof(A), types[0]);
    //            Assert.Equal(typeof(B), types[1]);
    //        }, () => {
    //            var types = _finder.Find<IC>();
    //            Assert.Equal(2, types.Count);
    //            Assert.Equal(typeof(B), types[0]);
    //            Assert.Equal(typeof(D<>), types[1]);
    //        });
    //    }

    //    /// <summary>
    //    /// 测试 获取程序集
    //    /// </summary>
    //    [Fact]
    //    public void Test_GetAssemblies()
    //    {
    //        var assemblies = _finder.GetAssemblies();
    //        assemblies.ForEach(x =>
    //        {
    //            Output.WriteLine(x.FullName);
    //        });
    //    }
    //}

    /// <summary>
    /// 测试查找接口
    /// </summary>
    public interface IA
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public class A : IA, IB
    {
    }

    /// <summary>
    /// 测试查找接口
    /// </summary>
    public interface IB
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public class B : IB, IC
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public abstract class C : IB, IC
    {
    }

    /// <summary>
    /// 测试查找接口
    /// </summary>
    public interface IC
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public class D<T> : IC
    {
    }

    /// <summary>
    /// 测试查找接口
    /// </summary>
    public interface IG<T>
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public class E : IG<E>
    {
    }

    /// <summary>
    /// 测试类型
    /// </summary>
    public class F2<T> : IG<T>
    {
    }
}
