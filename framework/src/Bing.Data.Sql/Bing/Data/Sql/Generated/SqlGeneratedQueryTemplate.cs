using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;

namespace Bing.Data.Sql;

/// <summary>完整 fluent 调用点的生成结构；调用方继续使用普通查询 API。</summary>
/// <remarks>仅供生成代码使用。缓存有界且不保存请求值、查询对象或执行资源。</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlGeneratedQueryTemplate
{
    /// <summary>编译期识别的单个操作。静态委托复用现有生成桥接和 Provider 规则。</summary>
    public sealed class Operation
    {
        /// <summary>创建生成操作，值槽和条件槽指向本次调用的参数数组。</summary>
        public Operation(Action<SqlLambdaQuery, object[]> replay, Func<object[], object> value = null,
            Func<object[], Operator> comparison = null, int valueArgument = -1, int conditionArgument = -1)
        {
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Value = value;
            Comparison = comparison;
            ValueArgument = valueArgument;
            ConditionArgument = conditionArgument;
        }

        internal Action<SqlLambdaQuery, object[]> Replay { get; }
        internal Func<object[], object> Value { get; }
        internal Func<object[], Operator> Comparison { get; }
        internal int ValueArgument { get; }
        internal int ConditionArgument { get; }
        internal bool Enabled(object[] arguments) => ConditionArgument < 0 || (bool)arguments[ConditionArgument];
    }

    private readonly Operation[] _operations;
    private readonly bool _replacesDefaultProjection;
    // 服务包按 Query 创建；按共享映射快照分桶，弱键避免静态调用点延长宿主生命周期。
    private readonly ConditionalWeakTable<IEntityMappingResolver, SqlQueryShapeCache<ShapeKey, CompiledShape>> _caches = new();

    /// <summary>创建完整调用点模板；不预先展开可选条件的组合。</summary>
    public SqlGeneratedQueryTemplate(Operation[] operations, bool replacesDefaultProjection = false)
    {
        _operations = (Operation[])(operations ?? throw new ArgumentNullException(nameof(operations))).Clone();
        _replacesDefaultProjection = replacesDefaultProjection;
    }

    /// <summary>记录当前请求的操作和值；不能缓存的 Builder 继续执行原桥接。</summary>
    public static SqlLambdaQuery Apply(SqlLambdaQuery source, SqlGeneratedQueryTemplate template, int operationIndex, object[] arguments)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (template == null) throw new ArgumentNullException(nameof(template));
        var core = source.GeneratedCore;
        var state = core.TemplateState;
        if (operationIndex == 0 && state == null && core.GetBuilder() is SqlBuilderBase builder &&
            builder.CanCacheGeneratedTemplate(template._replacesDefaultProjection))
            core.TemplateState = state = new State(source, template, builder);
        if (state != null && ReferenceEquals(state.Template, template) && state.Count == operationIndex)
            state.Add(arguments);
        else
        {
            core.MaterializeTemplate();
            template._operations[operationIndex].Replay(source, arguments);
        }
        return source;
    }

    private sealed record ShapeKey(ISqlProvider Provider, DatabaseType DatabaseType, ISqlPaginationRenderer Pagination,
        EntityMappingMetadata Mapping, SqlTableReference Table,
        string SourceAlias, bool AutomaticAliases, Type ResultType, string Arguments);

    private sealed record Binding(int Operation, int Item, string Name, Type EntityType, string Member, Operator? Comparison);

    private sealed class CompiledShape
    {
        internal SqlBuilderExecutionSnapshot Snapshot { get; init; }
        internal Binding[] Bindings { get; init; }
        internal Dictionary<string, int>[] Ordinals { get; init; }
    }

    /// <summary>每次查询独占参数和值；缓存只接收去除这些值后的结构。</summary>
    internal sealed class State
    {
        private readonly SqlLambdaQuery _query;
        private readonly SqlBuilderBase _builder;
        private readonly object[][] _arguments;
        private readonly object[] _values;
        private readonly Operator?[] _comparisons;
        private SqlBuilderExecutionSnapshot _lastSnapshot;
        private Type _lastResultType;
        private Dictionary<string, int>[] _lastOrdinals;

        internal State(SqlLambdaQuery query, SqlGeneratedQueryTemplate template, SqlBuilderBase builder)
        {
            _query = query;
            Template = template;
            _builder = builder;
            _arguments = new object[template._operations.Length][];
            _values = new object[_arguments.Length];
            _comparisons = new Operator?[_arguments.Length];
        }

        internal SqlGeneratedQueryTemplate Template { get; }
        internal int Count { get; private set; }

        internal void Add(object[] arguments)
        {
            var index = Count;
            var operation = Template._operations[index];
            if (operation.Enabled(arguments) && operation.Value != null)
            {
                var comparison = operation.Comparison(arguments);
                var value = operation.Value(arguments);
                if (comparison is Operator.In or Operator.NotIn && operation.ValueArgument >= 0)
                {
                    if (value is string or byte[] || value != null && value is not IEnumerable)
                        throw new ArgumentException("In 和 NotIn 条件值必须是非字符串、非二进制的 IEnumerable。", "value");
                    // 一次枚举，冷渲染和热绑定均消费同一份集合快照。
                    value = value == null ? Array.Empty<object>() : ((IEnumerable)value).Cast<object>().ToArray();
                    arguments[operation.ValueArgument] = value;
                }
                _values[index] = value;
                _comparisons[index] = comparison;
            }
            _arguments[index] = arguments;
            Count++;
        }

        internal void Replay(SqlLambdaQuery query)
        {
            for (var index = 0; index < Count; index++) Template._operations[index].Replay(query, _arguments[index]);
        }

        internal bool TryPrepare(Type resultType, out SqlBuilderExecutionSnapshot snapshot, out Dictionary<string, int>[] ordinals)
        {
            snapshot = null;
            ordinals = null;
            if (Count != _arguments.Length) return false;
            if (_lastSnapshot != null && _lastResultType == resultType)
            {
                snapshot = _lastSnapshot;
                ordinals = _lastOrdinals;
                return true;
            }
            // Contains 对集合沿用兼容语义，不把未知的集合类型反射判断放进模板热路径。
            if (_comparisons.Where((comparison, index) => comparison == Operator.Contains &&
                _values[index] is IEnumerable and not string and not byte[]).Any()) return false;
            var context = _builder.QueryClauseContext;
            var source = ((FromClause)((ISqlQueryClauseAccessor)_builder).FromClause).Sources.Single();
            var mapping = context.Services.EntityMappingResolver.Resolve(source.EntityType, context.ExecutionContext.DatabaseContext);
            var key = new ShapeKey(context.Provider, _builder.ResolveProviderDatabaseType(), _builder.FrozenPaginationRenderer,
                mapping, source.Reference, source.Alias,
                context.AutoGenerateTableAlias, resultType, ArgumentShape());
            // 默认验证器也可注入不同能力档案，当前查询仍须执行表引用校验。
            context.Services.TableReferenceValidator.Validate(source.Reference, _builder.ResolveProviderDatabaseType());
            var cache = Template._caches.GetValue(context.Services.EntityMappingResolver,
                static _ => new SqlQueryShapeCache<ShapeKey, CompiledShape>(256));
            SqlBuilderExecutionSnapshot coldSnapshot = null;
            var shape = cache.GetOrAdd(key, () => Compile(resultType, out coldSnapshot));
            if (coldSnapshot != null) snapshot = coldSnapshot;
            else
            {
                // 使用当前请求的参数工厂和上下文完成转换与限额检查，不复用首个请求的 Provider 值。
                var parameters = context.ParameterManager.CreateEmpty();
                var helper = new Helper(context, parameters);
                foreach (var binding in shape.Bindings)
                {
                    var value = binding.Item < 0 ? _values[binding.Operation] : ((object[])_values[binding.Operation])[binding.Item];
                    helper.AddParameter(parameters, binding.Name, value, binding.Comparison, binding.EntityType,
                        binding.Member, SqlParameterSource.Lambda);
                }
                snapshot = shape.Snapshot.WithParameters(((IAdvancedParameterManager)parameters).GetSqlParams().Values);
            }
            ordinals = shape.Ordinals;
            _lastResultType = resultType;
            _lastSnapshot = snapshot;
            _lastOrdinals = ordinals;
            return true;
        }

        private string ArgumentShape()
        {
            var shape = new StringBuilder();
            for (var index = 0; index < Count; index++)
            {
                var operation = Template._operations[index];
                var arguments = _arguments[index];
                if (!operation.Enabled(arguments)) { shape.Append("off;"); continue; }
                shape.Append("on;");
                for (var slot = 0; slot < arguments.Length; slot++)
                {
                    if (slot == operation.ValueArgument) continue;
                    var value = Convert.ToString(arguments[slot], CultureInfo.InvariantCulture);
                    shape.Append(value?.Length ?? -1).Append(':').Append(value).Append(';');
                }
                shape.Append(_comparisons[index] is Operator.In or Operator.NotIn ? ((object[])_values[index]).Length :
                    _values[index] == null ? -1 : -2).Append(';');
            }
            return shape.ToString();
        }

        private CompiledShape Compile(Type resultType, out SqlBuilderExecutionSnapshot boundSnapshot)
        {
            var query = SqlQueryRuntimeFactory.CreateLambdaQuery(_query.GeneratedCore.Executor, _builder.Clone());
            var bindings = new List<Binding>();
            var previousCount = 0;
            for (var index = 0; index < Count; index++)
            {
                Template._operations[index].Replay(query, _arguments[index]);
                var parameters = ((IAdvancedParameterManager)((SqlBuilderBase)query.GeneratedCore.ReadBuilder).ParameterManager).GetSqlParams().Values.ToArray();
                for (var parameter = previousCount; parameter < parameters.Length; parameter++)
                {
                    var item = parameters[parameter];
                    var collection = _comparisons[index] is Operator.In or Operator.NotIn;
                    bindings.Add(new Binding(index, collection ? parameter - previousCount : -1, item.Name,
                        item.EntityType, item.PropertyName, collection ? null : _comparisons[index]));
                }
                previousCount = parameters.Length;
            }
            var builder = (SqlBuilderBase)query.GeneratedCore.ReadBuilder;
            var select = (SelectClause)((ISqlQueryClauseAccessor)builder).SelectClause;
            select.ValidateStructure();
            Dictionary<string, int>[] ordinals = null;
            if (resultType != typeof(void))
            {
                select.ValidateResult(resultType, null, "Id");
                var layout = select.ProjectionLayout();
                if (layout.Count == 0) layout = select.EntityColumns(((FromClause)((ISqlQueryClauseAccessor)builder).FromClause).Sources.Single(), false);
                var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < layout.Count; index++)
                {
                    var column = layout[index].ToColumn(select.ProjectionContext);
                    columns.Add(column.ColumnAlias ?? column.Name, index);
                }
                ordinals = new[] { columns };
            }
            boundSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(builder);
            var slots = boundSnapshot.Parameters.ToArray();
            foreach (var slot in slots) { slot.Value = null; slot.OriginalValue = null; }
            return new CompiledShape { Snapshot = boundSnapshot.WithParameters(slots), Bindings = bindings.ToArray(), Ordinals = ordinals };
        }
    }
}
