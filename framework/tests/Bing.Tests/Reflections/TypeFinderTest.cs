//using Bing.Helpers;
//using Bing.Reflection;
//using Xunit;
//using Xunit.Abstractions;

//namespace Bing.Tests.Reflections
//{
//    /// <summary>
//    /// 类型查找器测试
//    /// </summary>
//    public class TypeFinderTest : TestBase
//    {
//        /// <summary>
//        /// 类型查找器
//        /// </summary>
//        private readonly ITypeFinder _typeFinder;

//        /// <summary>
//        /// 初始化一个<see cref="TypeFinderTest"/>类型的实例
//        /// </summary>
//        public TypeFinderTest(ITestOutputHelper output) : base(output)
//        {
//            _typeFinder = new TypeFinder(new AppDomainAllAssemblyFinder());
//        }

//        /// <summary>
//        /// 查找类型集合
//        /// </summary>
//        [Fact]
//        public void Test_Find()
//        {
//            var types = _typeFinder.Find<IA>();
//            Assert.Single(types);
//            Assert.Equal(typeof(A), types[0]);
//        }

//        /// <summary>
//        /// 查找类型集合
//        /// </summary>
//        [Fact]
//        public void Test_Find_2()
//        {
//            var types = _typeFinder.Find<IB>();
//            Assert.Equal(2, types.Count);
//            Assert.Equal(typeof(A), types[0]);
//            Assert.Equal(typeof(B), types[1]);
//        }

//        /// <summary>
//        /// 查找的结果类型包含泛型
//        /// </summary>
//        [Fact]
//        public void Test_Find_3()
//        {
//            var types = _typeFinder.Find<IC>();
//            Assert.Equal(2, types.Count);
//            Assert.Equal(typeof(B), types[0]);
//            Assert.Equal(typeof(D<>), types[1]);
//        }

//        /// <summary>
//        /// 查找泛型类型
//        /// </summary>
//        [Fact]
//        public void Test_Find_4()
//        {
//            var types = _typeFinder.Find<IG<E>>();
//            Assert.Single(types);
//            Assert.Equal(typeof(E), types[0]);
//        }

//        /// <summary>
//        /// 查找泛型类型
//        /// </summary>
//        [Fact]
//        public void Test_Find_5()
//        {
//            var types = _typeFinder.Find(typeof(IG<>));
//            Assert.Equal(2, types.Count);
//            Assert.Equal(typeof(E), types[0]);
//            Assert.Equal(typeof(F2<>), types[1]);
//        }

//        /// <summary>
//        /// 测试并发
//        /// </summary>
//        [Fact]
//        public void Test_Find_6()
//        {
//            Thread.ParallelExecute(() =>
//            {
//                var types = _typeFinder.Find<IA>();
//                Assert.Single(types);
//                Assert.Equal(typeof(A), types[0]);
//            }, () =>
//            {
//                var types = _typeFinder.Find<IB>();
//                Assert.Equal(2, types.Count);
//                Assert.Equal(typeof(A), types[0]);
//                Assert.Equal(typeof(B), types[1]);
//            }, () =>
//            {
//                var types = _typeFinder.Find<IC>();
//                Assert.Equal(2, types.Count);
//                Assert.Equal(typeof(B), types[0]);
//                Assert.Equal(typeof(D<>), types[1]);
//            });
//        }
//    }
//}
