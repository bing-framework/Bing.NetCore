using Microsoft.Extensions.DependencyInjection;
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
    /// <exception cref="ArgumentNullException"></exception>
    public static SkyApmExtensions AddSqlQuery(this SkyApmExtensions extensions)
    {
        if (extensions == null)
            throw new ArgumentNullException(nameof(extensions));
        extensions.Services.AddSingleton<ITracingDiagnosticProcessor, SqlQueryTracingDiagnosticProcessor>();
        return extensions;
    }
}
