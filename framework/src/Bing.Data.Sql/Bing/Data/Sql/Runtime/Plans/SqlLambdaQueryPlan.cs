using System.Collections.ObjectModel;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// Lambda 查询来源的别名生成策略。
/// </summary>
public enum SqlAliasPolicy
{
    /// <summary>只使用调用方提供的显式别名。</summary>
    ExplicitOnly,

    /// <summary>优先使用显式别名，否则使用 Lambda 参数名。</summary>
    Automatic,

    /// <summary>使用查询作用域和来源序号生成稳定别名。</summary>
    GeneratedStable
}

/// <summary>
/// Lambda 查询中的一个稳定来源槽位。
/// </summary>
internal sealed class SqlLambdaSourceSlot
{
    /// <summary>创建来源槽位。</summary>
    internal SqlLambdaSourceSlot(int index, Type entityType, string lambdaParameterName,
        string explicitAlias = null, string sourceId = null, bool requiresQualification = true)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        LambdaParameterName = string.IsNullOrWhiteSpace(lambdaParameterName)
            ? throw new ArgumentException("Lambda 来源参数名不能为空。", nameof(lambdaParameterName))
            : lambdaParameterName.Trim();
        Index = index;
        ExplicitAlias = string.IsNullOrWhiteSpace(explicitAlias) ? null : explicitAlias.Trim();
        SourceId = string.IsNullOrWhiteSpace(sourceId) ? "source_" + index : sourceId.Trim();
        RequiresQualification = requiresQualification;
    }

    /// <summary>来源在 Lambda 参数列表中的序号。</summary>
    internal int Index { get; }

    /// <summary>来源实体类型。</summary>
    internal Type EntityType { get; }

    /// <summary>Lambda 参数名称。</summary>
    internal string LambdaParameterName { get; }

    /// <summary>调用方显式指定的别名。</summary>
    internal string ExplicitAlias { get; }

    /// <summary>跨查询组合复用的来源身份。</summary>
    internal string SourceId { get; }

    /// <summary>生成列引用时是否需要限定来源。</summary>
    internal bool RequiresQualification { get; }
}

/// <summary>
/// Lambda 列引用的不可变描述。
/// </summary>
internal readonly record struct SqlLambdaColumnReference
{
    /// <summary>创建 Lambda 列引用。</summary>
    internal SqlLambdaColumnReference(string sourceId, string memberName, string resultAlias = null)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("列来源标识不能为空。", nameof(sourceId))
            : sourceId.Trim();
        MemberName = string.IsNullOrWhiteSpace(memberName)
            ? throw new ArgumentException("列成员名称不能为空。", nameof(memberName))
            : memberName.Trim();
        ResultAlias = string.IsNullOrWhiteSpace(resultAlias) ? null : resultAlias.Trim();
    }

    /// <summary>来源身份。</summary>
    internal string SourceId { get; }

    /// <summary>CLR 成员名称。</summary>
    internal string MemberName { get; }

    /// <summary>结果列别名。</summary>
    internal string ResultAlias { get; }
}

/// <summary>
/// Lambda 查询操作的不可变描述。
/// </summary>
internal sealed class SqlLambdaOperation
{
    /// <summary>创建 Lambda 查询操作。</summary>
    internal SqlLambdaOperation(string name, IEnumerable<SqlLambdaColumnReference> columns = null,
        string predicate = null)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Lambda 操作名称不能为空。", nameof(name))
            : name.Trim();
        var values = (columns ?? Array.Empty<SqlLambdaColumnReference>()).ToArray();
        Columns = new ReadOnlyCollection<SqlLambdaColumnReference>(values);
        Predicate = string.IsNullOrWhiteSpace(predicate) ? null : predicate.Trim();
    }

    /// <summary>操作名称。</summary>
    internal string Name { get; }

    /// <summary>操作引用的列。</summary>
    internal IReadOnlyList<SqlLambdaColumnReference> Columns { get; }

    /// <summary>可选的谓词文本。</summary>
    internal string Predicate { get; }
}

/// <summary>
/// Lambda 查询的不可变逻辑计划。
/// </summary>
internal sealed class SqlLambdaQueryPlan
{
    /// <summary>创建 Lambda 查询计划。</summary>
    internal SqlLambdaQueryPlan(IEnumerable<SqlLambdaSourceSlot> sources,
        IEnumerable<SqlLambdaOperation> operations = null,
        SqlQueryCorrelationKind correlation = SqlQueryCorrelationKind.Independent,
        SqlAliasPolicy aliasPolicy = SqlAliasPolicy.Automatic)
    {
        var sourceValues = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
        if (sourceValues.Length == 0) throw new ArgumentException("Lambda 查询至少需要一个来源。", nameof(sources));
        if (sourceValues.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() != sourceValues.Length)
            throw new ArgumentException("Lambda 查询来源标识必须唯一。", nameof(sources));
        if (sourceValues.Select(source => source.Index).Distinct().Count() != sourceValues.Length)
            throw new ArgumentException("Lambda 查询来源序号必须唯一。", nameof(sources));
        Sources = new ReadOnlyCollection<SqlLambdaSourceSlot>(sourceValues);
        Operations = new ReadOnlyCollection<SqlLambdaOperation>((operations ?? Array.Empty<SqlLambdaOperation>())
            .Where(operation => operation != null).ToArray());
        Correlation = correlation;
        AliasPolicy = aliasPolicy;
    }

    /// <summary>查询来源槽位。</summary>
    internal IReadOnlyList<SqlLambdaSourceSlot> Sources { get; }

    /// <summary>查询操作。</summary>
    internal IReadOnlyList<SqlLambdaOperation> Operations { get; }

    /// <summary>查询是否依赖外层作用域。</summary>
    internal SqlQueryCorrelationKind Correlation { get; }

    /// <summary>查询来源的别名策略。</summary>
    internal SqlAliasPolicy AliasPolicy { get; }

    /// <summary>在指定查询作用域中创建别名解析器。</summary>
    internal SqlAliasScope CreateAliasScope(SqlQueryScope scope = null) =>
        new(scope ?? new SqlQueryScope("lambda"), AliasPolicy, Sources);
}

/// <summary>
/// Lambda 查询来源的不可变别名作用域。
/// </summary>
internal sealed class SqlAliasScope
{
    private readonly ReadOnlyDictionary<string, string> _aliases;

    /// <summary>创建别名作用域。</summary>
    internal SqlAliasScope(SqlQueryScope scope, SqlAliasPolicy policy,
        IEnumerable<SqlLambdaSourceSlot> sources)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Policy = policy;
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources ?? throw new ArgumentNullException(nameof(sources)))
        {
            if (aliases.ContainsKey(source.SourceId))
                throw new ArgumentException("来源标识必须唯一。", nameof(sources));
            var alias = source.ExplicitAlias;
            if (string.IsNullOrWhiteSpace(alias) && policy != SqlAliasPolicy.ExplicitOnly)
            {
                alias = policy == SqlAliasPolicy.GeneratedStable
                    ? scope.Alias(source.LambdaParameterName, source.Index)
                    : source.LambdaParameterName;
            }
            aliases.Add(source.SourceId, string.IsNullOrWhiteSpace(alias) ? null : alias);
        }
        _aliases = new ReadOnlyDictionary<string, string>(aliases);
    }

    /// <summary>查询作用域。</summary>
    internal SqlQueryScope Scope { get; }

    /// <summary>使用的别名策略。</summary>
    internal SqlAliasPolicy Policy { get; }

    /// <summary>来源标识到最终别名的映射。</summary>
    internal IReadOnlyDictionary<string, string> Aliases => _aliases;

    /// <summary>解析来源别名。</summary>
    internal string Resolve(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("来源标识不能为空。", nameof(sourceId));
        return _aliases.TryGetValue(sourceId, out var alias)
            ? alias
            : throw new KeyNotFoundException("查询别名作用域中不存在来源：" + sourceId);
    }
}

/// <summary>投影结果的构造方式。</summary>
internal enum SqlProjectionConstruction
{
    /// <summary>使用公开无参构造和 Setter。</summary>
    ParameterlessSetter,

    /// <summary>使用生成的构造函数参数。</summary>
    Constructor,

    /// <summary>使用匿名对象或成员初始化表达式。</summary>
    MemberInitializer,

    /// <summary>使用调用方提供的实例作为投影原型。</summary>
    ExistingInstance
}

/// <summary>投影成员默认来源绑定。</summary>
internal sealed class SqlProjectionSourceBinding
{
    /// <summary>创建来源绑定。</summary>
    internal SqlProjectionSourceBinding(string sourceId, Type sourceType = null)
    {
        SourceId = string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException("投影来源标识不能为空。", nameof(sourceId))
            : sourceId.Trim();
        SourceType = sourceType;
    }

    /// <summary>来源标识。</summary>
    internal string SourceId { get; }

    /// <summary>来源实体类型。</summary>
    internal Type SourceType { get; }
}

/// <summary>投影成员的不可变绑定描述。</summary>
internal sealed class SqlProjectionMember
{
    /// <summary>创建投影成员。</summary>
    internal SqlProjectionMember(string targetMemberName, string sourceMemberName, string sourceId,
        string resultAlias, Type targetType, bool isNullable = false, bool isIgnored = false,
        SqlProjectionAggregate aggregate = null)
    {
        TargetMemberName = Require(targetMemberName, nameof(targetMemberName));
        SourceMemberName = string.IsNullOrWhiteSpace(sourceMemberName) ? null : sourceMemberName.Trim();
        SourceId = Require(sourceId, nameof(sourceId));
        ResultAlias = string.IsNullOrWhiteSpace(resultAlias) ? TargetMemberName : resultAlias.Trim();
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        IsNullable = isNullable;
        IsIgnored = isIgnored;
        Aggregate = aggregate;
    }

    /// <summary>目标成员名称。</summary>
    internal string TargetMemberName { get; }

    /// <summary>来源成员名称。</summary>
    internal string SourceMemberName { get; }

    /// <summary>来源标识。</summary>
    internal string SourceId { get; }

    /// <summary>结果列别名。</summary>
    internal string ResultAlias { get; }

    /// <summary>目标成员类型。</summary>
    internal Type TargetType { get; }

    /// <summary>目标成员是否可空。</summary>
    internal bool IsNullable { get; }

    /// <summary>目标成员是否忽略。</summary>
    internal bool IsIgnored { get; }

    /// <summary>可选的聚合描述。</summary>
    internal SqlProjectionAggregate Aggregate { get; }

    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("投影成员名称不能为空。", name)
        : value.Trim();
}

/// <summary>投影聚合节点的不可变描述。</summary>
internal sealed class SqlProjectionAggregate
{
    /// <summary>创建投影聚合节点。</summary>
    internal SqlProjectionAggregate(SqlAggregateFunction function, SqlLambdaColumnReference argument,
        bool distinct, string resultAlias, Type resultType)
    {
        if (!Enum.IsDefined(typeof(SqlAggregateFunction), function))
            throw new ArgumentOutOfRangeException(nameof(function));
        Function = function;
        Argument = argument;
        Distinct = distinct;
        ResultAlias = string.IsNullOrWhiteSpace(resultAlias) ? null : resultAlias.Trim();
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    /// <summary>聚合函数。</summary>
    internal SqlAggregateFunction Function { get; }

    /// <summary>聚合参数列。</summary>
    internal SqlLambdaColumnReference Argument { get; }

    /// <summary>是否去重。</summary>
    internal bool Distinct { get; }

    /// <summary>结果列别名。</summary>
    internal string ResultAlias { get; }

    /// <summary>结果类型。</summary>
    internal Type ResultType { get; }
}

/// <summary>投影形状的不可变描述。</summary>
internal sealed class SqlProjectionShape
{
    /// <summary>创建投影形状。</summary>
    internal SqlProjectionShape(Type targetType, IEnumerable<SqlProjectionMember> members,
        SqlProjectionConstruction construction, SqlProjectionSourceBinding sourceBinding,
        IEnumerable<SqlProjectionAggregate> aggregates = null)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        var values = (members ?? throw new ArgumentNullException(nameof(members)))
            .Where(member => member != null).ToArray();
        if (values.Length == 0) throw new ArgumentException("投影形状至少需要一个成员。", nameof(members));
        if (values.Select(member => member.TargetMemberName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
            throw new ArgumentException("投影目标成员必须唯一。", nameof(members));
        Members = new ReadOnlyCollection<SqlProjectionMember>(values);
        Construction = construction;
        SourceBinding = sourceBinding ?? throw new ArgumentNullException(nameof(sourceBinding));
        Aggregates = new ReadOnlyCollection<SqlProjectionAggregate>((aggregates ?? Array.Empty<SqlProjectionAggregate>())
            .Where(aggregate => aggregate != null).ToArray());
    }

    /// <summary>投影目标类型。</summary>
    internal Type TargetType { get; }

    /// <summary>投影成员。</summary>
    internal IReadOnlyList<SqlProjectionMember> Members { get; }

    /// <summary>结果构造方式。</summary>
    internal SqlProjectionConstruction Construction { get; }

    /// <summary>默认来源绑定。</summary>
    internal SqlProjectionSourceBinding SourceBinding { get; }

    /// <summary>投影中的聚合节点。</summary>
    internal IReadOnlyList<SqlProjectionAggregate> Aggregates { get; }
}
