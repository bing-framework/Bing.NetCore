using System.Collections.Generic;
using Bing.Helpers;
using Bing.Mapping;
using Bing.Tests.Samples;
using Xunit;

namespace Bing.AutoMapper.Tests
{
    /// <summary>
    /// AutoMapper 对象映射测试
    /// </summary>
    public class MapTest
    {
        public MapTest()
        {
            var mapper = new AutoMapperMapper();
            MapperExtensions.SetMapper(mapper);
        }

        /// <summary>
        /// 测试映射
        /// </summary>
        [Fact]
        public void Test_MapTo_1()
        {
            var sample = new Sample();
            var sample2 = new Sample2() { StringValue = "a" };
            sample2.MapTo(sample);
            Assert.Equal("a", sample.StringValue);
        }

        /// <summary>
        /// 测试映射
        /// </summary>
        [Fact]
        public void Test_MapTo_2()
        {
            var sample = new Sample() { StringValue = "a" };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("a", sample2.StringValue);
        }

        /// <summary>
        /// 测试映射
        /// </summary>
        [Fact]
        public void Test_MapTo_3()
        {
            var sample = new Sample { StringValue = "a" };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("a", sample2.StringValue);

            sample2 = new Sample2 { StringValue = "b" };
            sample = sample2.MapTo<Sample>();
            Assert.Equal("b", sample.StringValue);

            sample = new Sample { StringValue = "c" };
            sample2 = sample.MapTo<Sample2>();
            Assert.Equal("c", sample2.StringValue);
        }

        /// <summary>
        /// 测试映射 - 映射相同属性名的不同对象
        /// </summary>
        [Fact]
        public void Test_MapTo_4()
        {
            var sample = new Sample { Test3 = new Sample3Copy { StringValue = "a" } };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("a", sample2.Test3.StringValue);
        }

        /// <summary>
        /// 测试映射 - 映射相同属性名的不同对象集合
        /// </summary>
        [Fact]
        public void Test_MapTo_5()
        {
            var sample = new Sample { TestList = new List<Sample3Copy> { new Sample3Copy { StringValue = "a" }, new Sample3Copy { StringValue = "b" } } };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal(2, sample2.TestList.Count);
            Assert.Equal("a", sample2.TestList[0].StringValue);
            Assert.Equal("b", sample2.TestList[1].StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List_1()
        {
            var sampleList = new List<Sample>() { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            var sample2List = new List<Sample2>();
            sampleList.MapTo(sample2List);
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List_2()
        {
            var sampleList = new List<Sample>() { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            var sample2List = sampleList.MapTo<List<Sample2>>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试映射集合
        /// </summary>
        [Fact]
        public void Test_MapToList()
        {
            var sampleList = new List<Sample>() { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 映射集合 - 测试空集合
        /// </summary>
        [Fact]
        public void Test_MapToList_Empty()
        {
            var sampleList = new List<Sample>();
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Empty(sample2List);
        }

        /// <summary>
        /// 映射集合 - 测试数组
        /// </summary>
        [Fact]
        public void Test_MapToList_Array()
        {
            var sampleList = new Sample[] { new Sample() { StringValue = "a" }, new Sample() { StringValue = "b" } };
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 并发测试
        /// </summary>
        [Fact]
        public void Test_MapTo_MultipleThread()
        {
            Thread.ParallelExecute(() => {
                var sample = new Sample { StringValue = "a" };
                var sample2 = sample.MapTo<Sample2>();
                Assert.Equal("a", sample2.StringValue);
            }, 20);
        }

        /// <summary>
        /// 测试忽略特性
        /// </summary>
        [Fact]
        public void Test_MapTo_Ignore()
        {
            var sample2 = new DtoSample { Name = "a", IgnoreValue = "b" };
            var sample = sample2.MapTo<EntitySample>();
            Assert.Equal("a", sample.Name);
            Assert.Null(sample.IgnoreValue);
            var sample3 = sample.MapTo<DtoSample>();
            Assert.Equal("a", sample3.Name);
            Assert.Null(sample3.IgnoreValue);
        }

        /// <summary>
        /// 测试Castle代理类
        /// </summary>
        [Fact]
        public void Test_MapTo_CastleProxy()
        {
            var proxyGenerator = new Castle.DynamicProxy.ProxyGenerator();
            var proxy = proxyGenerator.CreateClassProxy<DtoSample>();
            proxy.Name = "a";
            var sample = proxy.MapTo<EntitySample>();
            Assert.Equal("a", sample.Name);

            var proxy2 = proxyGenerator.CreateClassProxy<DtoSample>();
            proxy2.Name = "b";
            sample = proxy2.MapTo<EntitySample>();
            Assert.Equal("b", sample.Name);

            var sample2 = new DtoSample { Name = "c" };
            var proxy3 = proxyGenerator.CreateClassProxy<EntitySample>();
            sample2.MapTo(proxy3);
            Assert.Equal("c", proxy3.Name);
        }
    }
}
