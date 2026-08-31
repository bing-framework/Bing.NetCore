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
    /// <returns>注册 SQL 查询跟踪后的 SkyAPM 扩展对象。</returns>
    /// <exception cref="ArgumentNullException">扩展对象为空时抛出。</exception>
    public static SkyApmExtensions AddSqlQuery(this SkyApmExtensions extensions)
    {
        if (extensions == null)
            throw new ArgumentNullException(nameof(extensions));
        extensions.Services.AddSingleton<ITracingDiagnosticProcessor, SqlQueryTracingDiagnosticProcessor>();
        return extensions;
    }
}
