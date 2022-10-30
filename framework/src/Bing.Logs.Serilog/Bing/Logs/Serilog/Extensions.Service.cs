using System;
using Bing.Logs.Abstractions;
using Bing.Logs.Core;
using Bing.Logs.Formats;
using Bing.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

namespace Bing.Logs.Serilog;

/// <summary>
/// 日志扩展
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// 注册Serilog日志操作
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configAction">日志配置</param>
    public static void AddSerilog(this IServiceCollection services, Action<LoggerConfiguration> configAction = null)
    {
        if (configAction != null)
        {
            var configuration = new LoggerConfiguration();
            configAction.Invoke(configuration);
        }
        else
        {
            SerilogProvider.InitConfiguration();
        }
        services.TryAddSingleton<ILogProviderFactory, Bing.Logs.Serilog.LogProviderFactory>();
        services.TryAddSingleton<ILogFormat, ContentFormat>();
        services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
        services.TryAddScoped<ILog, Log>();
    }

    /// <summary>
    /// 注册Serilog日志操作。使用日志工厂，实现混合日志
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">名称</param>
    /// <param name="configAction">日志配置</param>
    public static void AddSerilogWithFactory(this IServiceCollection services, string name = LogConst.DefaultSerilogName, Action<LoggerConfiguration> configAction = null)
    {
        if (configAction != null)
        {
            var configuration = new LoggerConfiguration();
            configAction.Invoke(configuration);
        }
        else
        {
            SerilogProvider.InitConfiguration();
        }
        services.TryAddSingleton<ILogFormat, ContentFormat>();
        services.TryAddScoped<ILogContext, Bing.Logs.Core.LogContext>();
        services.AddSingleton<ILogFactory, DefaultLogFactory>();
        services.AddScoped<ILog, Log>(x =>
        {
            var format = x.GetService<ILogFormat>();
            var provider = new LogProviderFactory().Create(name, format);
            var context = x.GetService<ILogContext>();
            var currentUser = x.GetService<ICurrentUser>();
            return new Log(name, provider, context, currentUser, "");
        });
    }
}