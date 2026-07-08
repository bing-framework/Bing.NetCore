using System.Diagnostics;
using AutoMapper;
using Bing.ObjectMapping;
using Bing.Reflection;

namespace Bing.AutoMapper;

/// <summary>
/// AutoMapper 配置验证测试
/// </summary>
public class AutoMapperConfigurationTest
{
    /// <summary>
    /// 创建配置好的 MapperConfiguration（包含所有 IObjectMapperProfile 实现）
    /// </summary>
    private static MapperConfiguration CreateConfiguration()
    {
        var allAssemblyFinder = new AppDomainAllAssemblyFinder();
        var profileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);
        var instances = profileTypeFinder
            .FindAll()
            .Select(t => Reflections.CreateInstance<IObjectMapperProfile>(t))
            .ToList();

        var config = new MapperConfiguration(cfg =>
        {
            foreach (var instance in instances)
            {
                Debug.WriteLine($"Profile: {instance.GetType().FullName}");
                instance.CreateMap();
                cfg.AddProfile(instance as Profile);
            }
        });
        return config;
    }

    #region Profile 发现

    /// <summary>
    /// 测试目的：MapperProfileTypeFinder 应在当前 AppDomain 中发现 TestMapperConfiguration。
    /// </summary>
    [Fact]
    public void ProfileTypeFinder_ShouldDiscoverTestMapperConfiguration()
    {
        // Arrange
        var allAssemblyFinder = new AppDomainAllAssemblyFinder();
        var profileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);

        // Act
        var profileTypes = profileTypeFinder.FindAll();

        // Assert
        profileTypes.ShouldNotBeNull();
        profileTypes.ShouldContain(typeof(TestMapperConfiguration));
    }

    /// <summary>
    /// 测试目的：发现的所有 Profile 类型均实现了 IObjectMapperProfile 接口。
    /// </summary>
    [Fact]
    public void DiscoveredProfiles_ShouldAllImplementIObjectMapperProfile()
    {
        // Arrange
        var allAssemblyFinder = new AppDomainAllAssemblyFinder();
        var profileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);

        // Act
        var profileTypes = profileTypeFinder.FindAll();

        // Assert
        foreach (var type in profileTypes)
        {
            type.IsAssignableTo(typeof(IObjectMapperProfile)).ShouldBeTrue(
                $"{type.FullName} 应实现 IObjectMapperProfile 接口");
        }
    }

    #endregion

    #region 配置有效性验证

    /// <summary>
    /// 测试目的：AssertConfigurationIsValid 不抛出异常，表明所有注册的 Profile 映射配置均有效。
    /// </summary>
    [Fact]
    public void AssertConfigurationIsValid_ShouldNotThrow()
    {
        // Arrange
        var config = CreateConfiguration();

        // Act & Assert
        Should.NotThrow(() => config.AssertConfigurationIsValid());
    }

    /// <summary>
    /// 测试目的：创建 AutoMapperObjectMapper 实例不应抛出异常（包含所有 Profile）。
    /// </summary>
    [Fact]
    public void CreateAutoMapperObjectMapper_ShouldNotThrow()
    {
        // Arrange
        var allAssemblyFinder = new AppDomainAllAssemblyFinder();
        var profileTypeFinder = new MapperProfileTypeFinder(allAssemblyFinder);
        var instances = profileTypeFinder
            .FindAll()
            .Select(t => Reflections.CreateInstance<IObjectMapperProfile>(t))
            .ToList();
        var config = new MapperConfiguration(cfg =>
        {
            foreach (var instance in instances)
            {
                instance.CreateMap();
                cfg.AddProfile(instance as Profile);
            }
        });

        // Act & Assert
        Should.NotThrow(() => new AutoMapperObjectMapper(config, instances));
    }

    #endregion

    #region 自定义 Profile 映射正确性

    /// <summary>
    /// 测试目的：TestMapperConfiguration 中 Sample → Sample4 的自定义映射（StringValue + "-1"）应正确执行。
    /// </summary>
    [Fact]
    public void CustomMapping_SampleToSample4_ShouldApplyTransform()
    {
        // Arrange
        var config = CreateConfiguration();
        var mapper = config.CreateMapper();

        var source = new Sample { StringValue = "hello" };

        // Act
        var target = mapper.Map<Sample4>(source);

        // Assert
        target.ShouldNotBeNull();
        target.StringValue.ShouldBe("hello-1");
    }

    /// <summary>
    /// 测试目的：TestMapperConfiguration 中 AutoMapperSourceSample → AutoMapperTargetSample 的映射应正确执行。
    /// </summary>
    [Fact]
    public void CustomMapping_SourceToTarget_ShouldApplyTransform()
    {
        // Arrange
        var config = CreateConfiguration();
        var mapper = config.CreateMapper();

        var source = new AutoMapperSourceSample { SourceStringValue = "666" };

        // Act
        var target = mapper.Map<AutoMapperTargetSample>(source);

        // Assert
        target.ShouldNotBeNull();
        target.TargetSampleValue.ShouldBe("666-001");
    }

    /// <summary>
    /// 测试目的：源属性值为 null 时，映射目标应保持 null 而非抛出异常。
    /// </summary>
    [Fact]
    public void CustomMapping_NullSourceValue_ShouldMapToNullTarget()
    {
        // Arrange
        var config = CreateConfiguration();
        var mapper = config.CreateMapper();

        var source = new AutoMapperSourceSample { SourceStringValue = null };

        // Act
        var target = mapper.Map<AutoMapperTargetSample>(source);

        // Assert
        target.ShouldNotBeNull();
        target.TargetSampleValue.ShouldBe("-001");
    }

    #endregion

    #region IObjectMapperProfile 接口方法

    /// <summary>
    /// 测试目的：TestMapperConfiguration.CreateMap() 可被独立调用而不抛出异常。
    /// </summary>
    [Fact]
    public void TestMapperConfiguration_CreateMap_ShouldNotThrow()
    {
        // Arrange
        var profile = new TestMapperConfiguration();

        // Act & Assert
        Should.NotThrow(() => profile.CreateMap());
    }

    /// <summary>
    /// 测试目的：TestMapperConfiguration 同时实现 Profile 和 IObjectMapperProfile 接口。
    /// </summary>
    [Fact]
    public void TestMapperConfiguration_ShouldImplementBothInterfaces()
    {
        // Arrange & Act
        var profile = new TestMapperConfiguration();

        // Assert
        profile.ShouldBeAssignableTo<Profile>();
        profile.ShouldBeAssignableTo<IObjectMapperProfile>();
    }

    #endregion
}
