﻿using Bing.Helpers;
using System.IO;
using Bing.Localization.Resources.ResourceTypes;

namespace Bing.Localization.Tests;

/// <summary>
/// 路径解析器测试
/// </summary>
public class PathResolverTest
{
    /// <summary>
    /// 路径解析器
    /// </summary>
    private readonly PathResolver _resolver;

    /// <summary>
    /// 输出小侠
    /// </summary>
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// 测试初始化
    /// </summary>
    public PathResolverTest(ITestOutputHelper output)
    {
        _output = output;
        _resolver = new PathResolver();
    }

    /// <summary>
    /// 测试 - 获取根命名空间 - 未设置RootNamespace特性，返回程序集名称
    /// </summary>
    [Fact]
    public void Test_GetRootNamespace_AssemblyName()
    {
        var type = typeof(Resource101);
        Assert.Equal("Bing.Localization.Resources", _resolver.GetRootNamespace(type.Assembly));
    }

    /// <summary>
    /// 测试 - 获取根命名空间 - 设置RootNamespace特性，返回特性值
    /// </summary>
    [Fact]
    public void Test_GetRootNamespace_RootNamespace()
    {
        var type = typeof(PathResolverTest);
        Assert.Equal("Bing.Localization", _resolver.GetRootNamespace(type.Assembly));
    }

    /// <summary>
    /// 测试 - 获取资源根目录路径 - 未设置ResourceLocation特性，返回传入的资源根路径
    /// </summary>
    [Fact]
    public void Test_GetResourcesRootPath_ResourcesPath()
    {
        var type = typeof(PathResolverTest);
        Assert.Equal("Resources", _resolver.GetResourcesRootPath(type.Assembly, "Resources"));
    }

    /// <summary>
    /// 测试 - 获取资源根目录路径 - 设置ResourceLocation特性，返回特性值
    /// </summary>
    [Fact]
    public void Test_GetResourcesRootPath_ResourceLocation()
    {
        var type = typeof(Resource101);
        Assert.Equal("Resources2", _resolver.GetResourcesRootPath(type.Assembly, "Resources"));
    }

    /// <summary>
    /// 测试 - 获取资源基名称 - 未设置RootNamespace特性
    /// </summary>
    [Fact]
    public void Test_GetResourcesBaseName_1()
    {
        var type = typeof(Resource101);
        Assert.Equal("ResourceTypes.Resource101", _resolver.GetResourcesBaseName(type.Assembly, type.FullName));
    }

    /// <summary>
    /// 测试 - 获取资源基名称 - 设置RootNamespace特性
    /// </summary>
    [Fact]
    public void Test_GetResourcesBaseName_2()
    {
        var type = typeof(PathResolverTest);
        Assert.Equal("Tests.PathResolverTest", _resolver.GetResourcesBaseName(type.Assembly, type.FullName));
    }

    /// <summary>
    /// 测试 - 获取Json资源文件绝对路径
    /// </summary>
    [Fact]
    public void Test_GetJsonResourcePath()
    {
        var result = Path.Combine(Common.ApplicationBaseDirectory, "Resources", "Tests.PathResolverTest.zh-CN.json");
        var path = _resolver.GetJsonResourcePath("Resources", "Tests.PathResolverTest", new CultureInfo("zh-CN"));
        _output.WriteLine(path);
        Assert.Equal(result, path);
    }

    /// <summary>
    /// 测试 - 获取Json资源文件绝对路径 - 内部类带+
    /// </summary>
    [Fact]
    public void Test_GetJsonResourcePath_InnerClass()
    {
        var result = Path.Combine(Common.ApplicationBaseDirectory, "Resources", "ResourceTypes.Resource4.Resource41.zh-CN.json");
        var path = _resolver.GetJsonResourcePath("Resources", "ResourceTypes.Resource4+Resource41", new CultureInfo("zh-CN"));
        _output.WriteLine(path);
        Assert.Equal(result, path);
    }

    /// <summary>
    /// 测试 - 获取Json资源文件绝对路径 - 基名称为空
    /// </summary>
    [Fact]
    public void Test_GetJsonResourcePath_NullBaseName()
    {
        var result = Path.Combine(Common.ApplicationBaseDirectory, "Resources", "zh-CN.json");
        var path = _resolver.GetJsonResourcePath("Resources", null, new CultureInfo("zh-CN"));
        _output.WriteLine(path);
        Assert.Equal(result, path);
    }
}
