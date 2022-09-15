using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Bing.Extensions;
using Bing.Helpers;
using Bing.ObjectMapping;
using Bing.Reflection;
using Bing.SampleClasses;
using Xunit;

namespace Bing.AutoMapper
{
    /// <summary>
    /// 对象映射器扩展测试
    /// </summary>
    public class ObjectMapperExtensionsTest
    {
        /// <summary>
        /// 测试初始化
        /// </summary>
        public ObjectMapperExtensionsTest()
        {
            var allAssemblyFinder = new AppDomainAllAssemblyFinder();
            var mapperProfileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);
            var instances = mapperProfileTypeFinder
                .FindAll()
                .Select(type => Reflections.CreateInstance<IObjectMapperProfile>(type))
                .ToList();
            var configuration = new MapperConfiguration(cfg =>
            {
                foreach (var instance in instances)
                {

                    Debug.WriteLine($"初始化AutoMapper配置：{instance.GetType().FullName}");
                    instance.CreateMap();
                    // ReSharper disable once SuspiciousTypeConversion.Global
                    cfg.AddProfile(instance as Profile);
                }
            });
            var mapper = new AutoMapperObjectMapper(configuration, instances);
            ObjectMapperExtensions.SetMapper(mapper);
        }

        /// <summary>
        /// 测试 - 映射 - Sample -> Sample2 未在配置类中配置，将自动配置映射
        /// </summary>
        [Fact]
        public void Test_MapTo_1()
        {
            var sample = new Sample { StringValue = "a" };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("a", sample2.StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 映射相同属性名的不同对象
        /// </summary>
        [Fact]
        public void Test_MapTo_2()
        {
            var sample = new Sample { Test3 = new Sample3Copy { StringValue = "a" } };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("a", sample2.Test3.StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 映射相同属性名的不同对象集合
        /// </summary>
        [Fact]
        public void Test_MapTo_3()
        {
            var sample = new Sample { TestList = new List<Sample3Copy> { new() { StringValue = "a" }, new() { StringValue = "b" } } };
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal(2, sample2.TestList.Count);
            Assert.Equal("a", sample2.TestList[0].StringValue);
            Assert.Equal("b", sample2.TestList[1].StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - Sample -> Sample4 已在配置类中配置映射关系
        /// </summary>
        [Fact]
        public void Test_MapTo_4()
        {
            var sample = new Sample { StringValue = "a" };
            var sample4 = sample.MapTo<Sample4>();
            Assert.Equal("a-1", sample4.StringValue);
        }

        /// <summary>
        /// 测试 - 映射- 已在配置类中配置映射关系 - 带参数重载
        /// </summary>
        [Fact]
        public void Test_MapTo_CustomProfile_1()
        {
            var sample = new Sample { StringValue = "a" };
            var sample4 = new Sample4();
            sample.MapTo(sample4);
            Assert.Equal("a-1", sample4.StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 多次执行预配置映射
        /// </summary>
        [Fact]
        public void Test_MapTo_CustomProfile_2()
        {
            var sample = new Sample { StringValue = "a" };
            var sample4 = sample.MapTo<Sample4>();
            Assert.Equal("a-1", sample4.StringValue);

            sample.StringValue = "b";
            sample4 = sample.MapTo<Sample4>();
            Assert.Equal("b-1", sample4.StringValue);

            sample.StringValue = "c";
            sample4 = sample.MapTo<Sample4>();
            Assert.Equal("c-1", sample4.StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 预配置映射与自动配置映射混合执行
        /// </summary>
        [Fact]
        public void Test_MapTo_CustomProfile_3()
        {
            var sample = new Sample { StringValue = "a" };
            var sample4 = sample.MapTo<Sample4>();
            Assert.Equal("a-1", sample4.StringValue);

            sample.StringValue = "b";
            var sample2 = sample.MapTo<Sample2>();
            Assert.Equal("b", sample2.StringValue);

            sample.StringValue = "c";
            sample4 = sample.MapTo<Sample4>();
            Assert.Equal("c-1", sample4.StringValue);
        }

        /// <summary>
        /// 测试 - 映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List_1()
        {
            var sampleList = new List<Sample> { new() { StringValue = "a" }, new() { StringValue = "b" } };
            var sample2List = new List<Sample2>();
            sampleList.MapTo(sample2List);
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试 - 映射集合
        /// </summary>
        [Fact]
        public void Test_MapTo_List_2()
        {
            var sampleList = new List<Sample> { new() { StringValue = "a" }, new() { StringValue = "b" } };
            var sample2List = sampleList.MapTo<List<Sample2>>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 并发测试
        /// </summary>
        [Fact]
        public void Test_MapTo_MultipleThread()
        {
            Thread.ParallelExecute(() =>
            {
                var sample = new Sample { StringValue = "a" };
                var sample2 = sample.MapTo<Sample2>();
                Assert.Equal("a", sample2.StringValue);
            }, 20);
        }

        /// <summary>
        /// 测试 - 映射 - 忽略特性
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
        /// 测试 - 映射 - Castle代理类
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

        /// <summary>
        /// 测试 - 映射集合
        /// </summary>
        [Fact]
        public void Test_MapToList_1()
        {
            var sampleList = new List<Sample> { new() { StringValue = "a" }, new() { StringValue = "b" } };
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试 - 映射集合 - 测试空集合
        /// </summary>
        [Fact]
        public void Test_MapToList_2()
        {
            var sampleList = new List<Sample>();
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Empty(sample2List);
        }

        /// <summary>
        /// 测试 - 映射集合 - 测试数组
        /// </summary>
        [Fact]
        public void Test_MapToList_3()
        {
            Sample[] sampleList = { new() { StringValue = "a" }, new() { StringValue = "b" } };
            var sample2List = sampleList.MapToList<Sample2>();
            Assert.Equal(2, sample2List.Count);
            Assert.Equal("a", sample2List[0].StringValue);
        }

        /// <summary>
        /// 测试 - 映射 - 失败映射抛出异常
        /// </summary>
        [Fact]
        public void Test_MapTo_Throw()
        {
            var parentId = Guid.NewGuid().SafeString();
            var dto = new TreeEntityDto { Name = "Test1", ParentId = parentId };
            var entity = dto.MapTo<TreeEntitySample>();
            Assert.Equal(parentId, entity.ParentId.SafeString());

            //错误数据映射失败,抛出异常
            var parentId2 = "/";
            var dto2 = new TreeEntityDto { Name = "Test2", ParentId = parentId2 };
            Assert.Throws<AutoMapperMappingException>(() => dto2.MapTo<TreeEntitySample>());

            //成功映射,失败映射不影响之前的映射关系
            var entity3 = dto.MapTo<TreeEntitySample>();
            Assert.Equal(parentId, entity3.ParentId.SafeString());
        }
    }
}
