using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

internal static partial class BingPluginLoader
{
    /// <summary>
    /// 将文件路径限制在插件目录中。
    /// </summary>
    /// <param name="directory">插件根目录。</param>
    /// <param name="path">待检查的文件路径。</param>
    /// <param name="manifestPath">用于错误诊断的清单路径。</param>
    private static void EnsureInsideDirectory(string directory, string path, string manifestPath)
    {
        var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("插件入口程序集不能离开插件目录: " + manifestPath);
    }

    /// <summary>
    /// 获取必填文本字段。
    /// </summary>
    /// <param name="value">字段原始文本。</param>
    /// <param name="field">字段名称。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>去除首尾空白的字段值。</returns>
    private static string RequireText(string value, string field, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("插件清单字段不能为空: " + field + "，清单: " + manifestPath);
        return value.Trim();
    }

    /// <summary>
    /// 解析三段或四段插件版本及可选的预发布和构建标识。
    /// </summary>
    /// <param name="value">版本文本。</param>
    /// <param name="field">版本字段名称。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>已解析的插件版本。</returns>
    private static NuGetVersion ParseVersion(string value, string field, string manifestPath)
    {
        var text = RequireText(value, field, manifestPath);
        var numeric = text.Split('-', '+')[0].Split('.');
        if (numeric.Length < 3 || numeric.Length > 4 ||
            numeric.Any(part => !int.TryParse(part, out var number) || number < 0) ||
            !NuGetVersion.TryParseStrict(text, out var version))
            throw new InvalidOperationException("插件版本必须是三段或四段非负数字，可带合法预发布标识: " + text + "，清单: " + manifestPath);
        return version;
    }

    /// <summary>
    /// 解析精确版本或 NuGet 区间约束。
    /// </summary>
    /// <param name="text">依赖版本要求。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>已解析的版本区间。</returns>
    private static VersionRange ParseVersionRequirement(string text, string manifestPath)
    {
        if (text[0] != '[' && text[0] != '(')
        {
            var exact = ParseVersion(text, "dependencies.version", manifestPath);
            return new VersionRange(exact, true, exact, true);
        }
        if (text.Length < 3 || (text[text.Length - 1] != ']' && text[text.Length - 1] != ')'))
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        var bounds = text.Substring(1, text.Length - 2).Split(',');
        if (bounds.Length < 1 || bounds.Length > 2)
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        foreach (var bound in bounds.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try { ParseVersion(bound.Trim(), "dependencies.version", manifestPath); }
            catch (InvalidOperationException error)
            {
                throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath, error);
            }
        }
        if (!VersionRange.TryParse(text, out var range) ||
            (range.MinVersion == null && range.MaxVersion == null) ||
            (range.MinVersion != null && range.MaxVersion != null &&
                (range.MinVersion.CompareTo(range.MaxVersion) > 0 ||
                 (range.MinVersion.CompareTo(range.MaxVersion) == 0 &&
                  (!range.IsMinInclusive || !range.IsMaxInclusive)))))
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        return range;
    }

    /// <summary>
    /// 从完整版本中提取兼容旧契约的三段或四段数值版本。
    /// </summary>
    /// <param name="text">完整版本文本。</param>
    /// <returns>三段或四段数值版本。</returns>
    private static Version ParseNumericVersion(string text) => Version.Parse(text.Split('-', '+')[0]);
}
