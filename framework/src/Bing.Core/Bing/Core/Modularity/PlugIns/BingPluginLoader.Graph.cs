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
    /// 按插件依赖关系排序并校验版本要求。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <returns>依赖优先的插件序列。</returns>
    private static List<PluginRecord> OrderPlugins(IReadOnlyList<PluginRecord> records)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            foreach (var dependency in record.Dependencies)
            {
                if (!byId.TryGetValue(dependency.Id, out var depended))
                {
                    var missingPath = FindDependencyPath(records, record.Id)
                        .Concat(new[] { dependency.Id });
                    throw AttachPluginContext(new InvalidOperationException(
                        "插件依赖不存在: " + string.Join(" -> ", missingPath)), record, "ValidateDependencies",
                        dependencyPath: missingPath);
                }
                if (!dependency.VersionRange.Satisfies(depended.Version) ||
                    (depended.Version.IsPrerelease && !dependency.AllowsPrerelease))
                {
                    var versionPath = FindDependencyPath(records, record.Id)
                        .Concat(new[] { dependency.Id });
                    throw AttachPluginContext(new InvalidOperationException(
                        "插件依赖版本不匹配: " + string.Join(" -> ", versionPath) + "，要求 " + dependency.Id + " " + dependency.VersionRequirement +
                        "，实际为 " + depended.VersionText), record, "ValidateDependencies",
                        dependencyPath: versionPath);
                }
            }
        }

        var ordered = new List<PluginRecord>(records.Count);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (ordered.Count < records.Count)
        {
            var ready = records.Where(record => !completed.Contains(record.Id) &&
                    record.Dependencies.All(dependency => completed.Contains(dependency.Id)))
                .OrderBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
            {
                var cycle = FindPluginCycle(records, completed);
                var cycleError = new InvalidOperationException("插件依赖存在环: " + string.Join(" -> ", cycle));
                var first = records.FirstOrDefault(record => string.Equals(record.Id, cycle[0], StringComparison.OrdinalIgnoreCase));
                if (first != null)
                    throw AttachPluginContext(cycleError, first, "ValidateDependencies", dependencyPath: cycle);
                throw cycleError;
            }

            foreach (var record in ready)
            {
                ordered.Add(record);
                completed.Add(record.Id);
            }
        }
        return ordered;
    }

    /// <summary>
    /// 查找插件依赖环并返回完整路径。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <param name="completed">已完成排序的插件标识。</param>
    /// <returns>插件依赖环路径。</returns>
    private static IReadOnlyList<string> FindPluginCycle(IEnumerable<PluginRecord> records, ISet<string> completed)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(completed, StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        foreach (var record in records.OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            if (visited.Contains(record.Id))
                continue;
            var cycle = Visit(record.Id);
            if (cycle != null)
                return cycle;
        }
        return records.Select(record => record.Id).ToArray();

        IReadOnlyList<string> Visit(string id)
        {
            if (visited.Contains(id))
                return null;
            if (visiting.Contains(id))
            {
                return path.Concat(new[] { id }).ToArray();
            }

            visiting.Add(id);
            path.Add(id);
            foreach (var dependency in byId[id].Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var cycle = Visit(dependency.Id);
                if (cycle != null)
                    return cycle;
            }
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            return null;
        }
    }

    /// <summary>
    /// 查找从插件图入口到指定插件的依赖路径。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <param name="targetId">目标插件 ID。</param>
    /// <returns>包含目标插件的依赖路径；无法从入口到达时返回目标插件自身。</returns>
    private static IReadOnlyList<string> FindDependencyPath(IReadOnlyList<PluginRecord> records, string targetId)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        var dependedOn = new HashSet<string>(records.SelectMany(record => record.Dependencies)
            .Where(dependency => byId.ContainsKey(dependency.Id))
            .Select(dependency => dependency.Id), StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var roots = records.Where(record => !dependedOn.Contains(record.Id))
            .OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
        var starts = roots.Length > 0 ? roots : records.OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
        foreach (var root in starts)
        {
            if (Visit(root.Id))
                return path.ToArray();
        }
        return new[] { targetId };

        bool Visit(string id)
        {
            if (path.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
                return false;
            path.Add(id);
            if (string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var dependency in byId[id].Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (byId.ContainsKey(dependency.Id) && Visit(dependency.Id))
                    return true;
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }
}
