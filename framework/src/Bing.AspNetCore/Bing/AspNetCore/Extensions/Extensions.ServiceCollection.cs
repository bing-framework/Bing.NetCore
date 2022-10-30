using System;
using Bing.AspNetCore.Logs;
using Bing.AspNetCore.Mvc;
using Bing.AspNetCore.Uploads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.AspNetCore.Extensions;

/// <summary>
/// 服务集合(<see cref="IServiceCollection"/>)扩展
/// </summary>
public static class BingServiceCollectionExtensions
{
    /// <summary>
    /// 注册上传服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddUploadService(this IServiceCollection services) => services.AddUploadService<DefaultFileUploadService>();

    /// <summary>
    /// 注册上传服务
    /// </summary>
    /// <typeparam name="TFileUploadService">文件上传服务类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddUploadService<TFileUploadService>(this IServiceCollection services) where TFileUploadService : class, IFileUploadService => services.TryAddScoped<IFileUploadService, TFileUploadService>();

    /// <summary>
    /// 注册Api接口服务
    /// </summary>
    /// <param name="services">服务集合</param>
    public static void AddApiInterfaceService(this IServiceCollection services) => services.AddApiInterfaceService<DefaultApiInterfaceService>();

    /// <summary>
    /// 注册Api接口服务
    /// </summary>
    /// <typeparam name="TApiInterfaceService">Api接口服务类型</typeparam>
    /// <param name="services">服务集合</param>
    public static void AddApiInterfaceService<TApiInterfaceService>(this IServiceCollection services) where TApiInterfaceService : class, IApiInterfaceService =>
        services.TryAddSingleton<IApiInterfaceService, TApiInterfaceService>();

    /// <summary>
    /// 注册请求响应日志服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static void AddRequestResponseLog(this IServiceCollection services, Action<RequestResponseLoggerOptions> setupAction)
    {
        AddRequestResponseLog<DefaultRequestResponseLogger, DefaultRequestResponseLogCreator>(services, setupAction);
    }

    /// <summary>
    /// 注册请求响应日志服务
    /// </summary>
    /// <typeparam name="TLogger">请求响应日志记录器</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static void AddRequestResponseLog<TLogger>(this IServiceCollection services, Action<RequestResponseLoggerOptions> setupAction)
        where TLogger : class, IRequestResponseLogger
    {
        AddRequestResponseLog<TLogger, DefaultRequestResponseLogCreator>(services, setupAction);
    }

    /// <summary>
    /// 注册请求响应日志服务
    /// </summary>
    /// <typeparam name="TLogger">请求响应日志记录器</typeparam>
    /// <typeparam name="TLogCreator">请求响应日志创建器</typeparam>
    /// <param name="services">服务集合</param>
    /// <param name="setupAction">配置操作</param>
    public static void AddRequestResponseLog<TLogger, TLogCreator>(this IServiceCollection services, Action<RequestResponseLoggerOptions> setupAction)
        where TLogger : class, IRequestResponseLogger
        where TLogCreator : class, IRequestResponseLogCreator
    {
        services.Configure(setupAction);
        services.AddSingleton<IRequestResponseLogger, TLogger>();
        services.AddScoped<IRequestResponseLogCreator, TLogCreator>();
    }
}