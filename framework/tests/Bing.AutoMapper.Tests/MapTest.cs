using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Bing.Helpers;
using Bing.ObjectMapping;
using Bing.Reflection;
using Bing.Tests.Samples;
using Xunit;
using Sample = Bing.SampleClasses.Sample;
using Sample2 = Bing.SampleClasses.Sample2;
using Sample3Copy = Bing.SampleClasses.Sample3Copy;

namespace Bing.AutoMapper.Tests;

/// <summary>
/// AutoMapper 对象映射测试
/// </summary>
public class MapTest
{
    public MapTest()
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
    /// 测试映射
    /// </summary>
    [Fact]
    public void Test_MapTo_1()
    {
        var sample = new Sample();
        var sample2 = new Sample2 { StringValue = "a" };
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
        Thread.ParallelExecute(() =>
        {
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

    /// <summary>
    /// 测试自定义配置
    /// </summary>
    [Fact]
    public void Test_MapTo_CustomProfile()
    {
        var source = new AutoMapperSourceSample { SourceStringValue = "666" };
        var target = source.MapTo<AutoMapperTargetSample>();
        Assert.Equal("666-001", target.TargetSampleValue);
    }

    /// <summary>
    /// 测试自定义配置及非自定义规则映射
    /// </summary>
    [Fact]
    public void Test_MapTo_CustomProfile_WithMoreRule()
    {
        var source = new AutoMapperSourceSample { SourceStringValue = "666" };
        var target = source.MapTo<AutoMapperTargetSample>();
        Assert.Equal("666-001", target.TargetSampleValue);

        var sample = new Sample();
        var sample2 = new Sample2() { StringValue = "a" };
        sample2.MapTo(sample);

        Assert.Equal("a", sample.StringValue);

        source = new AutoMapperSourceSample { SourceStringValue = "666" };
        target = source.MapTo<AutoMapperTargetSample>();
        Assert.Equal("666-001", target.TargetSampleValue);
    }

    [Fact]
    public void Test_MapTo_ListToList()
    {
        var mm = new List<T2>();
        mm.Add(new T2 { A = "123" });
        mm.Add(new T2 { A = "123" });
        mm.Add(new T2 { A = "123" });
        mm.Add(new T2 { A = "123" });
        var ff2 = mm.MapTo<List<T2Dto>>();

        var t1 = new T1();
        t1.A = "123123";
        t1.T2 = new List<T2>();
        t1.T2.Add(new T2 { A = "123" });
        t1.T2.Add(new T2 { A = "123" });
        t1.T2.Add(new T2 { A = "123" });
        t1.T2.Add(new T2 { A = "123" });
        t1.T2.Add(new T2 { A = "123" });
        var ff1 = t1.MapTo<T1Dto>();
    }

    public class T1
    {
        public string A { get; set; }

        public List<T2> T2 { get; set; }
    }

    public class T2
    {
        public string A { get; set; }
    }

    public class T1Dto
    {
        public string A { get; set; }

        public List<T2Dto> T2 { get; set; }
    }

    public class T2Dto
    {
        public string A { get; set; }
    }
}