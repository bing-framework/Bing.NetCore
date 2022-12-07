using System.Diagnostics;
using System.Linq;
using AutoMapper;
using Bing.ObjectMapping;
using Bing.Reflection;
using Bing.SampleClasses;
using Xunit;

namespace Bing.AutoMapper;

/// <summary>
/// 对象映射器测试
/// </summary>
public class ObjectMapperTest
{
    /// <summary>
    /// 对象映射器
    /// </summary>
    private readonly Bing.ObjectMapping.IObjectMapper _mapper;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public ObjectMapperTest()
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
        _mapper = new AutoMapperObjectMapper(configuration, instances);
    }

    /// <summary>
    /// 测试 - 映射 - Sample -> Sample2 已经配置类中配置映射关系
    /// </summary>
    [Fact]
    public void Test_Map_1()
    {
        var sample = new Sample { StringValue = "a" };
        var sample2 = _mapper.Map<Sample, Sample2>(sample);
        Assert.Equal("a", sample2.StringValue);
    }

    /// <summary>
    /// 测试 - 映射 - Sample -> Sample2 已经配置类中配置映射关系 - 两参数重载
    /// </summary>
    [Fact]
    public void Test_Map_2()
    {
        var sample = new Sample { StringValue = "a" };
        var sample2 = new Sample2();
        _mapper.Map(sample, sample2);
        Assert.Equal("a", sample2.StringValue);
    }

    /// <summary>
    /// 测试 - 映射 - Sample2 -> Sample 未在配置类中配置，将自动配置映射
    /// </summary>
    [Fact]
    public void Test_Map_3()
    {
        var sample2 = new Sample2 { StringValue = "a" };
        var sample = _mapper.Map<Sample2, Sample>(sample2);
        Assert.Equal("a", sample.StringValue);
    }

    /// <summary>
    /// 测试 - 映射 - 用于重现动态配置后映射之前的配置出现问题
    /// 1. 执行Sample -> Sample2映射,已在配置类中配置映射关系
    /// 2. 执行Sample2 -> Sample映射,未在配置类中配置,将自动配置映射
    /// 3. 重复执行Sample -> Sample2映射
    /// </summary>
    [Fact]
    public void Test_Map_4()
    {
        var sample = new Sample { StringValue = "a" };
        var sample2 = _mapper.Map<Sample, Sample2>(sample);
        Assert.Equal("a", sample2.StringValue);

        var sample3 = _mapper.Map<Sample2, Sample>(sample2);
        Assert.Equal("a", sample3.StringValue);

        var sample4 = _mapper.Map<Sample, Sample2>(sample);
        Assert.Equal("a", sample4.StringValue);
    }
}