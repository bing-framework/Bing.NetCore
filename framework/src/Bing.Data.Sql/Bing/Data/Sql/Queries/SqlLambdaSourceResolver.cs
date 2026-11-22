using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;

namespace Bing.Data.Sql;

/// <summary>
/// 按显式别名、参数名和唯一类型绑定查询来源。
/// </summary>
internal static class SqlLambdaSourceResolver
{
    /// <summary>
    /// 获取查询中的所有 From 和 Join 数据源。
    /// </summary>
    /// <param name="accessor">提供查询子句访问能力的对象。</param>
    /// <returns>按 From 和 Join 顺序组成的数据源列表。</returns>
    internal static IReadOnlyList<TableSource> Sources(ISqlQueryClauseAccessor accessor) =>
        ((ISqlMultiSourceFromClause)accessor.FromClause).Sources
        .Concat(((ISqlMultiSourceJoinClause)accessor.JoinClause).TypedSources).ToList();

    /// <summary>
    /// 解析查询输入对应的数据源列表。
    /// </summary>
    /// <param name="accessor">提供查询子句访问能力的对象。</param>
    /// <param name="expression">参数类型表示实体类型的 Lambda 表达式。</param>
    /// <param name="aliases">可选的数据源别名列表。</param>
    /// <param name="entityCount">可选的实体参数数量；为 null 时使用表达式参数数量。</param>
    /// <returns>按 Lambda 参数顺序解析的数据源列表。</returns>
    /// <remarks>
    /// 表达式为空、别名数量不匹配、同一数据源被重复绑定或无法唯一解析数据源时会抛出异常。
    /// </remarks>
    internal static IReadOnlyList<TableSource> Resolve(ISqlQueryClauseAccessor accessor,
        LambdaExpression expression, IReadOnlyList<string> aliases = null, int? entityCount = null)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var count = entityCount ?? expression.Parameters.Count;
        return Resolve(accessor, expression.Parameters.Take(count).Select(parameter => parameter.Type).ToArray(),
            expression.Parameters.Take(count).Select(parameter => parameter.Name).ToArray(), aliases);
    }

    /// <summary>
    /// 解析查询输入对应的数据源列表。
    /// </summary>
    /// <param name="accessor">提供查询子句访问能力的对象。</param>
    /// <param name="types">实体参数类型数组。</param>
    /// <param name="names">对应的 Lambda 参数名称数组。</param>
    /// <param name="aliases">可选的数据源别名数组。</param>
    /// <returns>按类型数组顺序解析并绑定的数据源列表。</returns>
    /// <remarks>
    /// 名称或别名数量与类型数量不匹配、数据源无法唯一匹配，或 Provider 不支持绑定生成的别名时会抛出异常。
    /// </remarks>
    internal static IReadOnlyList<TableSource> Resolve(ISqlQueryClauseAccessor accessor, Type[] types,
        string[] names, IReadOnlyList<string> aliases = null)
    {
        var count = types.Length;
        if (names.Length != count || aliases != null && aliases.Count != count)
            throw new ArgumentException("来源名称及显式别名数量必须与实体参数一致。");
        var result = new List<TableSource>(count);
        for (var index = 0; index < count; index++)
        {
            var parameter = (Type: types[index], Name: names[index]);
            var source = Find(Sources(accessor), parameter.Type, aliases?[index], parameter.Name);
            if (result.Any(item => item.SourceId == source.SourceId))
                throw new InvalidOperationException($"参数 {parameter.Name} 重复绑定同一查询来源。");
            if (ShouldGenerateAlias(accessor) &&
                (string.IsNullOrWhiteSpace(source.Alias) || source.Reference != null && string.IsNullOrWhiteSpace(source.Reference.Alias)))
            {
                var name = AliasHint(parameter.Name);
                if (name != null)
                {
                    var previousAlias = source.Alias;
                    if (accessor.FromClause is FromClause from && from.Sources.Any(item => item.SourceId == source.SourceId))
                        source = from.BindSourceAlias(source.SourceId, name);
                    else if (accessor.JoinClause is JoinClause join)
                        source = join.BindSourceAlias(source.SourceId, name);
                    else
                        throw new NotSupportedException("当前 Provider 不支持 Lambda 来源命名。");
                    (accessor.SelectClause as SelectClause)?.RebindInferredAlias(source.EntityType, previousAlias, name);
                }
            }
            result.Add(source);
        }
        return result;
    }

    /// <summary>
    /// 在可用数据源中查找唯一匹配的实体数据源。
    /// </summary>
    /// <param name="available">候选数据源集合。</param>
    /// <param name="entityType">要查找的实体类型。</param>
    /// <param name="alias">可选的数据源别名。</param>
    /// <param name="parameterName">用于优先匹配别名并生成错误信息的 Lambda 参数名。</param>
    /// <returns>唯一匹配的实体数据源。</returns>
    /// <remarks>
    /// 显式别名优先匹配；未指定别名时优先使用与参数名匹配的数据源。找不到唯一匹配项时抛出异常。
    /// </remarks>
    internal static TableSource Find(IEnumerable<TableSource> available, Type entityType,
        string alias = null, string parameterName = null)
    {
        var candidates = available.Where(item => item.EntityType == entityType).ToList();
        var explicitAlias = !string.IsNullOrWhiteSpace(alias);
        if (explicitAlias)
            candidates = candidates.Where(item => string.Equals(item.Alias, alias, StringComparison.OrdinalIgnoreCase)).ToList();
        else if (!string.IsNullOrWhiteSpace(parameterName))
        {
            var named = candidates.Where(item => string.Equals(item.Alias, parameterName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count != 0)
                candidates = named;
        }
        if (candidates.Count != 1)
            throw new InvalidOperationException($"{(candidates.Count == 0 ? "未找到表达式参数对应来源；" : "")}查询来源不唯一。参数 {parameterName ?? alias ?? "(未命名)"}（{entityType.Name}）无法唯一绑定来源。候选别名：{string.Join(", ", candidates.Select(item => item.Alias ?? "(未命名)"))}。请使用实际别名作为参数名或显式指定 alias。");
        return candidates[0];
    }

    /// <summary>
    /// 获取当前查询是否启用数据源别名生成。
    /// </summary>
    /// <param name="accessor">提供查询子句访问能力的对象。</param>
    /// <returns>当前构建器或投影上下文启用别名生成时返回 true，否则返回 false；没有可用设置时默认为 true。</returns>
    private static bool ShouldGenerateAlias(ISqlQueryClauseAccessor accessor) =>
        accessor is SqlBuilderBase builder ? builder.AliasPolicy != SqlAliasPolicy.ExplicitOnly :
        (accessor.SelectClause as SelectClause)?.ProjectionContext.AutoGenerateTableAlias ?? true;

    /// <summary>
    /// 确定 Join 数据源使用的别名。
    /// </summary>
    /// <param name="accessor">提供查询子句访问能力的对象。</param>
    /// <param name="explicitAlias">调用方指定的显式别名。</param>
    /// <param name="parameterName">对应的 Lambda 参数名称。</param>
    /// <returns>显式别名；未指定时按查询设置生成的参数别名；关闭生成时为 null。</returns>
    internal static string JoinAlias(ISqlQueryClauseAccessor accessor, string explicitAlias, string parameterName) =>
        explicitAlias ?? (ShouldGenerateAlias(accessor) ? AliasHint(parameterName) : null);

    /// <summary>
    /// 将 Lambda 参数名称转换为有效的别名提示。
    /// </summary>
    /// <param name="name">要转换的参数名称。</param>
    /// <returns>有效的别名提示；名称为空白或为下划线时返回 null。</returns>
    /// <remarks>
    /// 名称包含非标识符字符时抛出参数异常。
    /// </remarks>
    internal static string AliasHint(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "_")
            return null;
        if (!(char.IsLetter(name[0]) || name[0] == '_') || name.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new ArgumentException($"Lambda 参数名 {name} 不是有效的表别名。");
        return name;
    }
}
