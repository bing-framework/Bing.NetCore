﻿using System.Reflection;
using Bing.Extensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bing.Localization.Json;

/// <summary>
/// Json本地化资源查找器工厂
/// </summary>
public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    /// <summary>
    /// 资源根目录路径
    /// </summary>
    private readonly string _rootPath;

    /// <summary>
    /// 路径解析器
    /// </summary>
    private readonly IPathResolver _pathResolver;

    /// <summary>
    /// 日志工厂
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// 缓存
    /// </summary>
    private readonly IMemoryCache _cache;

    /// <summary>
    /// 初始化一个<see cref="JsonStringLocalizerFactory"/>类型的实例
    /// </summary>
    /// <param name="options">本地化配置</param>
    /// <param name="pathResolver">路径解析器</param>
    /// <param name="loggerFactory">日志工厂</param>
    /// <param name="cache">缓存</param>
    /// <exception cref="ArgumentNullException"></exception>
    public JsonStringLocalizerFactory(IOptions<Microsoft.Extensions.Localization.LocalizationOptions> options, IPathResolver pathResolver, ILoggerFactory loggerFactory, IMemoryCache cache)
    {
        options.CheckNull(nameof(options));
        _rootPath = options.Value.ResourcesPath;
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _cache = cache;
    }

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource)
    {
        resourceSource.CheckNull(nameof(resourceSource));
        var assembly = resourceSource.Assembly;
        var rootPath = _pathResolver.GetResourcesRootPath(assembly, _rootPath);
        var baseName = _pathResolver.GetResourcesBaseName(assembly, resourceSource.FullName);
        return new JsonStringLocalizer(_pathResolver, rootPath, baseName, _loggerFactory.CreateLogger<JsonStringLocalizer>(), _cache);
    }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location)
    {
        location ??= new AssemblyName(GetType().Assembly.FullName).Name;
        var assemblyName = new AssemblyName(location);
        var assembly = Assembly.Load(assemblyName);
        var rootPath = _pathResolver.GetResourcesRootPath(assembly, _rootPath);
        return new JsonStringLocalizer(_pathResolver, rootPath, baseName, _loggerFactory.CreateLogger<JsonStringLocalizer>(), _cache);
    }
}
