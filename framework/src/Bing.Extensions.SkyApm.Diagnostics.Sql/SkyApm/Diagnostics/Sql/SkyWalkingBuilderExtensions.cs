using Microsoft.Extensions.DependencyInjection;
using SkyApm.Common;
using SkyApm.Config;
using SkyApm.Tracing;
using SkyApm.Utilities.DependencyInjection;

namespace SkyApm.Diagnostics.Sql;

/// <summary>
/// SkyApm构建器扩展
/// </summary>
public static class SkyWalkingBuilderExtensions
{
    /// <summary>
    /// 注册SqlQuery的SkyApm链路跟踪
    /// </summary>
    /// <param name="extensions">扩展</param>
    /// <param name="component">自定义设定SqlQuery组件，若未设定则默认采用SqlClient组件</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static SkyApmExtensions AddSqlQuery(this SkyApmExtensions extensions, StringOrIntValue? component = null)
    {
        if (extensions == null)
            throw new ArgumentNullException(nameof(extensions));
        extensions.Services.AddSingleton<ITracingDiagnosticProcessor>(sp =>
            new SqlQueryTracingDiagnosticProcessor(
                sp.GetRequiredService<ITracingContext>(),
                sp.GetRequiredService<IExitSegmentContextAccessor>(),
                sp.GetRequiredService<IConfigAccessor>(),
                component));
        return extensions;
    }
}
