using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Conditions;
using Bing.Data.Sql.Builders.Params;
using System.Text;
using Bing.Data.Sql.Builders.Extensions;
using Bing.Data.Sql.Builders.Internal;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Properties;

namespace Bing.Data.Sql.Builders.Clauses;

/// <summary>
/// 默认 SQL Provider 的 From 子句实现。
/// </summary>
public class FromClause : IFromClause
{
    /// <summary>
    /// 当前查询源的 SQL 项，兼容旧 Update From 路径。
    /// </summary>
    protected SqlItem Table;

    /// <summary>
    /// 查询图中的根表源实例。
    /// </summary>
    private readonly List<TableSource> _sources;

    /// <summary>
    /// 子句运行上下文。
    /// </summary>
    private readonly SqlClauseContext _context;

    /// <summary>
    /// SQL 方言。
    /// </summary>
    protected IDialect Dialect => _context.Dialect;

    /// <summary>
    /// 实体解析器。
    /// </summary>
    protected IEntityResolver Resolver => _context.EntityResolver;

    /// <summary>
    /// 实体别名注册器。
    /// </summary>
    protected IEntityAliasRegister Register => _context.AliasRegister;

    /// <summary>
    /// SQL 生成器。
    /// </summary>
    protected ISqlBuilder Builder => _context.Builder;

    /// <summary>
    /// SQL 对象名称格式化器。
    /// </summary>
    protected ISqlObjectNameFormatter ObjectNameFormatter => _context.Services.ObjectNameFormatter;

    /// <summary>
    /// 独立子句渲染结构化表引用时使用的固定数据库类型。
    /// </summary>
    protected readonly DatabaseType? ProviderDatabaseType;

    /// <summary>
    /// SQL 表引用验证器。
    /// </summary>
    protected ISqlTableReferenceValidator TableReferenceValidator => _context.Services.TableReferenceValidator;

    /// <summary>
    /// SQL 字符串表引用解析器。
    /// </summary>
    protected ISqlTableReferenceParser TableReferenceParser => _context.Provider.TableReferenceParser;

    /// <summary>
    /// 初始化绑定到指定运行上下文的 From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public FromClause(SqlClauseContext context)
        : this(context, null, context?.Provider.DatabaseType)
    {
    }

    /// <summary>
    /// 使用运行上下文初始化 From 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="table">表。</param>
    /// <param name="providerDatabaseType">独立子句使用的固定数据库类型。</param>
    protected FromClause(SqlClauseContext context, SqlItem table, DatabaseType? providerDatabaseType)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Table = table;
        ProviderDatabaseType = providerDatabaseType;
        _sources = table == null ? new List<TableSource>() : new List<TableSource>
        {
            new("source_0", table)
        };
    }

    /// <inheritdoc />
    public virtual IFromClause Clone(SqlClauseContext context)
    {
        if (context.AliasRegister != null)
            context.AliasRegister.FromType = Register.FromType;
        var sources = _sources.Select(source => source.Clone()).ToList();
        var clone = CreateClone(context, sources.LastOrDefault()?.Item);
        clone.SetSources(sources);
        return clone;
    }

    /// <summary>
    /// 创建克隆后的 From 子句。
    /// </summary>
    /// <param name="context">克隆 Builder 的运行上下文。</param>
    /// <param name="table">已复制的末尾表项。</param>
    /// <returns>保留 Provider 子类类型的 From 子句。</returns>
    protected virtual FromClause CreateClone(SqlClauseContext context, SqlItem table) =>
        new FromClause(context, table, ProviderDatabaseType);

    /// <inheritdoc />
    public void From(string table, string alias = null)
    {
        var parsedTable = ParseTableName(table, alias);
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = Register?.Clone();
        ReleaseSourceAliases(registerProbe);
        registerProbe?.RegisterAlias(parsedTable.Alias);
        _context.UseOperation(SqlOperationAction.QueryClause);
        ReleaseSourceAliases(Register);
        Register?.RegisterAlias(parsedTable.Alias);
        ReplaceSources(CreateSqlItem(parsedTable.TableName, parsedTable.Schema, parsedTable.Alias), alias: parsedTable.Alias);
    }

    /// <summary>
    /// 解析字符串表名及别名。
    /// </summary>
    /// <param name="table">表名。</param>
    /// <param name="alias">别名。</param>
    /// <returns>表名、别名和架构名。</returns>
    protected virtual (string TableName, string Alias, string Schema) ParseTableName(string table, string alias)
    {
        var parsedTable = TableReferenceParser.Parse(table, alias);
        return (parsedTable.TableName, parsedTable.Alias, parsedTable.Schema);
    }

    /// <inheritdoc />
    public void From(SqlTableReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var resolvedAlias = reference.EntityType == null ? reference.Alias :
            Resolver.GetAlias(reference.EntityType, reference.Alias);
        var registerProbe = Register?.Clone();
        ReleaseSourceAliases(registerProbe);
        if (reference.EntityType == null)
            registerProbe?.RegisterAlias(reference.Alias);
        else
            registerProbe?.Replace(reference.EntityType, resolvedAlias);
        var item = CreateStructuredSqlItem(reference);
        _context.UseOperation(SqlOperationAction.QueryClause);
        ReleaseSourceAliases(Register);
        if (reference.EntityType == null)
            Register?.RegisterAlias(reference.Alias);
        else
            Register?.Replace(reference.EntityType, resolvedAlias);
        ReplaceSources(item, reference.EntityType, resolvedAlias);
        if (reference.EntityType == null)
            return;
        if (Register != null)
            Register.FromType = reference.EntityType;
    }

    /// <summary>
    /// 创建保存字符串表引用的 SQL 项。
    /// </summary>
    /// <param name="table">已解析的表名。</param>
    /// <param name="schema">已解析的架构名。</param>
    /// <param name="alias">已解析的表别名。</param>
    /// <returns>用于默认方言渲染的 SQL 表项。</returns>
    protected virtual SqlItem CreateSqlItem(string table, string schema, string alias) =>
        SqlItem.Parse(table, schema, alias);

    /// <inheritdoc />
    public void From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        var type = typeof(TEntity);
        var reference = Resolver.GetTableReference(type) with { Alias = alias, EntityType = type };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
        From(reference);
    }

    /// <summary>
    /// 创建用于延迟格式化和校验的结构化表引用 SQL 项。
    /// </summary>
    /// <param name="reference">待渲染的结构化表引用。</param>
    /// <returns>绑定当前 Builder 服务和数据库上下文的结构化 SQL 项。</returns>
    protected virtual SqlItem CreateStructuredSqlItem(SqlTableReference reference)
    {
        var sqlBuilder = Builder as SqlBuilderBase;
        var databaseContext = sqlBuilder?.GetDatabaseContext();
        var databaseType = sqlBuilder?.ResolveProviderDatabaseType(reference) ?? ProviderDatabaseType;
        return new StructuredSqlItem(reference, ObjectNameFormatter, databaseContext, databaseType,
            TableReferenceValidator);
    }

    /// <summary>
    /// 获取当前类型化 From 使用的结构化表引用。
    /// </summary>
    /// <returns>结构化表引用；原始字符串 From 返回 <see langword="null"/>。</returns>
    internal SqlTableReference GetStructuredReference() => (Table as StructuredSqlItem)?.Reference;

    /// <summary>
    /// 获取当前查询图的根表源快照。
    /// </summary>
    internal IReadOnlyList<TableSource> Sources => _sources;

    /// <summary>
    /// 追加结构化实体根表源。
    /// </summary>
    /// <typeparam name="TEntity">来源实体类型。</typeparam>
    /// <param name="alias">来源别名。</param>
    /// <param name="schema">来源架构。</param>
    internal void AppendRoot<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        AppendRoot(typeof(TEntity), alias, schema);
    }

    /// <summary>
    /// 追加指定实体类型的结构化根表源。
    /// </summary>
    /// <param name="entityType">来源实体类型。</param>
    /// <param name="alias">来源别名。</param>
    /// <param name="schema">来源架构。</param>
    internal void AppendRoot(Type entityType, string alias = null, string schema = null)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));
        var reference = Resolver.GetTableReference(entityType) with { Alias = alias, EntityType = entityType };
        if (string.IsNullOrWhiteSpace(schema) == false)
            reference = reference with { Schema = schema };
        AppendRoot(reference);
    }

    /// <summary>
    /// 追加结构化根表源。
    /// </summary>
    /// <param name="reference">待追加的表引用。</param>
    internal void AppendRoot(SqlTableReference reference)
    {
        if (reference == null)
            throw new ArgumentNullException(nameof(reference));
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var resolvedAlias = reference.EntityType == null ? reference.Alias :
            Resolver.GetAlias(reference.EntityType, reference.Alias);
        var registerProbe = Register?.Clone();
        if (reference.EntityType == null)
            registerProbe?.RegisterAlias(reference.Alias);
        else
            registerProbe?.Register(reference.EntityType, resolvedAlias);
        var item = CreateStructuredSqlItem(reference);
        _context.UseOperation(SqlOperationAction.QueryClause);
        if (reference.EntityType == null)
            Register?.RegisterAlias(reference.Alias);
        else
            Register?.Register(reference.EntityType, resolvedAlias);
        _sources.Add(new TableSource($"source_{_sources.Count}", item, reference.EntityType, resolvedAlias));
        Table = item;
    }

    /// <summary>
    /// 使用参数位置绑定解析多表 Lambda 谓词。
    /// </summary>
    /// <param name="expression">多表谓词表达式。</param>
    /// <returns>可追加到 Where 子句的参数化条件。</returns>
    internal ICondition ResolveMultiSourcePredicate(System.Linq.Expressions.LambdaExpression expression) =>
        ResolveMultiSourcePredicate(expression, Sources);

    /// <summary>
    /// 使用指定查询图表源解析多表 Lambda 谓词。
    /// </summary>
    /// <param name="expression">多表谓词表达式。</param>
    /// <param name="sources">按 Lambda 参数顺序排列的表源实例。</param>
    /// <returns>可追加到 Where、Having 或 On 子句的参数化条件。</returns>
    internal ICondition ResolveMultiSourcePredicate(System.Linq.Expressions.LambdaExpression expression,
        IReadOnlyList<TableSource> sources)
    {
        return ResolveMultiSourcePredicate(expression, sources, _context.ParameterManager);
    }

    /// <summary>
    /// 使用指定参数管理器解析多表 Lambda 谓词。
    /// </summary>
    /// <param name="expression">多表谓词表达式。</param>
    /// <param name="sources">按 Lambda 参数顺序排列的表源实例。</param>
    /// <param name="parameterManager">接收本次解析参数的目标管理器。</param>
    /// <returns>可追加到 Where、Having 或 On 子句的参数化条件。</returns>
    /// <remarks>
    /// 调用方可传入参数副本，在完整解析成功后再通过 <see cref="MergeNewParameters"/> 提交，
    /// 以保持复合查询操作的原子性。
    /// </remarks>
    internal ICondition ResolveMultiSourcePredicate(System.Linq.Expressions.LambdaExpression expression,
        IReadOnlyList<TableSource> sources, IParameterManager parameterManager)
    {
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        var existingParameters = parameterManager.GetParams();
        var snapshot = parameterManager.Clone();
        var condition = new MultiSourcePredicateExpressionResolver(expression, sources, GetSqlColumn, snapshot)
            .Resolve(expression);
        var parameters = snapshot.GetParams()
            .Where(parameter => existingParameters.ContainsKey(parameter.Key) == false)
            .ToList();
        var validation = parameterManager.Clone();
        AddParameters(validation, snapshot, parameters);
        AddParameters(parameterManager, snapshot, parameters);
        return condition;
    }

    /// <summary>
    /// 将参数副本新增的参数合并到当前 Builder。
    /// </summary>
    /// <param name="parameterManager">包含候选解析结果的参数管理器。</param>
    internal void MergeNewParameters(IParameterManager parameterManager)
    {
        if (parameterManager == null)
            throw new ArgumentNullException(nameof(parameterManager));
        var existingParameters = _context.ParameterManager.GetParams();
        var parameters = parameterManager.GetParams()
            .Where(parameter => existingParameters.ContainsKey(parameter.Key) == false)
            .ToList();
        AddParameters(_context.ParameterManager, parameterManager, parameters);
    }

    /// <summary>
    /// 将解析副本中新建的参数提交到指定管理器。
    /// </summary>
    /// <param name="target">接收参数的管理器。</param>
    /// <param name="source">保存解析结果的参数管理器。</param>
    /// <param name="parameters">待提交的新参数。</param>
    private static void AddParameters(IParameterManager target, IParameterManager source,
        IEnumerable<KeyValuePair<string, object>> parameters)
    {
        var sourceMetadata = source as IAdvancedParameterManager;
        var targetMetadata = target as IAdvancedParameterManager;
        foreach (var parameter in parameters)
        {
            if (sourceMetadata?.GetSqlParams().TryGetValue(parameter.Key, out var metadata) == true &&
                targetMetadata != null)
            {
                targetMetadata.Add(metadata);
                continue;
            }
            target.Add(parameter.Key, parameter.Value);
        }
    }

    /// <summary>
    /// 解析多表投影表达式中的参数成员。
    /// </summary>
    /// <param name="expression">返回 object 数组的投影表达式。</param>
    /// <returns>按投影顺序排列的完整列 SQL。</returns>
    internal IReadOnlyList<string> ResolveMultiSourceColumns(System.Linq.Expressions.LambdaExpression expression) =>
        ResolveMultiSourceColumns(expression, Sources);

    /// <summary>
    /// 解析指定查询图表源的多表投影、分组或排序表达式。
    /// </summary>
    /// <param name="expression">返回列或列数组的表达式。</param>
    /// <param name="sources">按 Lambda 参数顺序排列的表源实例。</param>
    /// <returns>按表达式顺序排列的完整列 SQL。</returns>
    internal IReadOnlyList<string> ResolveMultiSourceColumns(System.Linq.Expressions.LambdaExpression expression,
        IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        var bindings = new SqlParameterBindingScope(expression, sources);
        IEnumerable<System.Linq.Expressions.Expression> columns = expression.Body is System.Linq.Expressions.NewArrayExpression array
            ? array.Expressions
            : new[] { expression.Body };
        var result = new List<string>();
        foreach (var column in columns)
        {
            var current = column is System.Linq.Expressions.UnaryExpression { NodeType: System.Linq.Expressions.ExpressionType.Convert } unary
                ? unary.Operand
                : column;
            if (bindings.TryGetSource(current, out var source) == false)
                throw new InvalidOperationException("多表投影中的列必须引用当前查询的 Lambda 参数。");
            result.Add(GetSqlColumn(current, source));
        }

        return result;
    }

    /// <summary>
    /// 根据指定表源解析单列 Lambda 表达式。
    /// </summary>
    /// <param name="expression">返回实体成员的列表达式。</param>
    /// <param name="source">显式绑定的查询表源。</param>
    /// <returns>按当前方言格式化的列 SQL。</returns>
    internal string ResolveMultiSourceColumn(System.Linq.Expressions.LambdaExpression expression, TableSource source)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return GetSqlColumn(UnwrapConversion(expression.Body), source);
    }

    /// <summary>
    /// 按指定表源解析单列值条件，并保留列映射元数据参数。
    /// </summary>
    /// <param name="expression">返回实体成员的列表达式。</param>
    /// <param name="source">显式绑定的表源实例。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <returns>包含显式来源列和元数据参数的条件。</returns>
    internal ICondition ResolveMultiSourceValueCondition(System.Linq.Expressions.LambdaExpression expression,
        TableSource source, object value, Operator @operator)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var member = UnwrapConversion(expression.Body);
        var rawColumn = Resolver.GetColumn(member, source.EntityType);
        var column = GetSqlColumn(member, source);
        return new Helper(_context).CreateCondition(rawColumn, column, source.EntityType, value, @operator);
    }

    /// <summary>
    /// 解析多表 DTO 成员初始化投影，并使用目标成员名作为结果列别名。
    /// </summary>
    /// <param name="expression">返回 DTO 成员初始化对象的多表投影表达式。</param>
    /// <param name="sources">按 Lambda 参数顺序排列的表源实例。</param>
    /// <returns>包含方言安全结果别名的投影列。</returns>
    internal IReadOnlyList<string> ResolveMultiSourceDtoColumns(System.Linq.Expressions.LambdaExpression expression,
        IReadOnlyList<TableSource> sources) => ResolveMultiSourceDtoColumns(expression, sources, out _);

    /// <summary>
    /// 解析多表 DTO 成员初始化投影，并返回可供派生表公开的成员名称。
    /// </summary>
    /// <param name="expression">返回 DTO 成员初始化对象的多表投影表达式。</param>
    /// <param name="sources">按 Lambda 参数顺序排列的表源实例。</param>
    /// <param name="projectedMembers">DTO 投影成员名称。</param>
    /// <returns>包含方言安全结果别名的投影列。</returns>
    internal IReadOnlyList<string> ResolveMultiSourceDtoColumns(System.Linq.Expressions.LambdaExpression expression,
        IReadOnlyList<TableSource> sources, out IReadOnlyCollection<string> projectedMembers)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        var body = UnwrapConversion(expression.Body);
        if (body is not System.Linq.Expressions.MemberInitExpression memberInit)
            throw new NotSupportedException("多表 DTO 投影必须使用成员初始化表达式。");
        if (memberInit.Bindings.Count == 0)
            throw new NotSupportedException("多表 DTO 投影至少需要一个成员初始化绑定。");

        var bindings = new SqlParameterBindingScope(expression, sources);
        var result = new List<string>();
        var members = new List<string>();
        foreach (var binding in memberInit.Bindings)
        {
            if (binding is not System.Linq.Expressions.MemberAssignment assignment)
                throw new NotSupportedException($"不支持的多表 DTO 投影绑定: {binding.BindingType}。");
            var sourceExpression = UnwrapConversion(assignment.Expression);
            if (IsDirectParameterMember(sourceExpression, expression.Parameters) == false ||
                bindings.TryGetSource(sourceExpression, out var source) == false)
                throw new NotSupportedException("多表 DTO 投影成员必须引用当前查询的 Lambda 参数。");
            result.Add($"{GetSqlColumn(sourceExpression, source)} As {Dialect.SafeName(assignment.Member.Name)}");
            members.Add(assignment.Member.Name);
        }

        projectedMembers = members;
        return result;
    }

    /// <summary>
    /// 解包表达式中的装箱或数值转换节点。
    /// </summary>
    /// <param name="expression">待处理的表达式。</param>
    /// <returns>移除转换节点后的表达式。</returns>
    private static System.Linq.Expressions.Expression UnwrapConversion(System.Linq.Expressions.Expression expression)
    {
        while (expression is System.Linq.Expressions.UnaryExpression
               {
                   NodeType: System.Linq.Expressions.ExpressionType.Convert or
                       System.Linq.Expressions.ExpressionType.ConvertChecked
               } conversion)
            expression = conversion.Operand;
        return expression;
    }

    /// <summary>
    /// 判断表达式是否为当前 Lambda 参数的直接成员访问。
    /// </summary>
    /// <param name="expression">待验证的成员表达式。</param>
    /// <param name="parameters">当前 Lambda 参数集合。</param>
    /// <returns>表达式为直接成员访问时返回 true。</returns>
    private static bool IsDirectParameterMember(System.Linq.Expressions.Expression expression,
        IReadOnlyList<System.Linq.Expressions.ParameterExpression> parameters) =>
        expression is System.Linq.Expressions.MemberExpression member &&
        parameters.Any(parameter => ReferenceEquals(UnwrapConversion(member.Expression), parameter));

    /// <summary>
    /// 根据已绑定的表源实例生成列 SQL。
    /// </summary>
    /// <param name="expression">待解析的实体成员表达式。</param>
    /// <param name="source">表达式绑定的表源实例。</param>
    /// <returns>按当前方言格式化的列 SQL。</returns>
    private string GetSqlColumn(System.Linq.Expressions.Expression expression, TableSource source)
    {
        var member = expression as System.Linq.Expressions.MemberExpression;
        string column;
        if (source.ProjectedMembers != null)
        {
            if (member == null || UnwrapConversion(member.Expression) is not System.Linq.Expressions.ParameterExpression ||
                source.ProjectedMembers.Contains(member.Member.Name) == false)
                throw new NotSupportedException("多表派生表只能引用已投影的 DTO 成员。");
            column = member.Member.Name;
        }
        else
            column = Resolver.GetColumn(expression, source.EntityType);
        var alias = source.Alias ?? source.Reference?.Alias ?? Resolver.GetAlias(source.EntityType, null);
        return new SqlItem(column, alias).ToSql(Dialect);
    }

    /// <inheritdoc />
    public void From(ISqlBuilder builder, string alias)
    {
        if (builder == null)
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = Register?.Clone();
        ReleaseSourceAliases(registerProbe);
        registerProbe?.RegisterAlias(alias);
        var subqueryAlias = GetSubqueryAlias(alias);
        var result = Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(builder) : builder.ToSql();
        _context.UseOperation(SqlOperationAction.QueryClause);
        ReleaseSourceAliases(Register);
        Register?.RegisterAlias(alias);
        ReplaceSources(SqlItem.Raw($"({result}){subqueryAlias}"), alias: alias);
    }

    /// <summary>
    /// 使用保留 DTO 投影成员与来源身份的类型化派生表替换根来源。
    /// </summary>
    /// <typeparam name="TProjection">派生表公开的 DTO 类型。</typeparam>
    /// <param name="subquery">已冻结的类型化派生表。</param>
    internal void From<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class
    {
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        subquery.ValidateCompatible(Builder);
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var registerProbe = Register?.Clone();
        ReleaseSourceAliases(registerProbe);
        registerProbe?.RegisterAlias(subquery.Alias);
        var subqueryAlias = GetSubqueryAlias(subquery.Alias);
        var sql = Builder is SqlBuilderBase sqlBuilder ? sqlBuilder.RenderSubquery(subquery.Builder) :
            subquery.Builder.ToSql();
        _context.UseOperation(SqlOperationAction.QueryClause);
        ReleaseSourceAliases(Register);
        Register?.RegisterAlias(subquery.Alias);
        ReplaceSources(SqlItem.Raw($"({sql}){subqueryAlias}"), typeof(TProjection), subquery.Alias,
            subquery.ProjectedMembers);
        (Builder as SqlBuilderBase)?.RegisterSubqueryParent(subquery.ParentQueryContextId);
    }

    /// <summary>
    /// 获取派生表别名的方言渲染文本。
    /// </summary>
    /// <param name="alias">派生表别名。</param>
    /// <returns>包含前导空格的别名 SQL 文本。</returns>
    protected virtual string GetSubqueryAlias(string alias) => $" As {Dialect.SafeName(alias)}";

    /// <inheritdoc />
    public void From(Action<ISqlBuilder> action, string alias)
    {
        if (action == null)
            return;
        _context.ValidateOperation(SqlOperationAction.QueryClause);
        var builder = Builder.New();
        action(builder);
        From(builder, alias);
    }

    /// <inheritdoc />
    public void AppendSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        _context.UseOperation(SqlOperationAction.QueryClause);
        if (Table != null && Table.IsRaw)
        {
            Table = SqlItem.Raw($"{Table.Name}{sql}");
            _sources[^1] = new TableSource(_sources[^1].SourceId, Table);
            return;
        }
        ReleaseSourceAliases(Register);
        ReplaceSources(SqlItem.Raw(sql));
    }

    /// <inheritdoc />
    public void Validate()
    {
        if (_sources.Count == 0 || _sources.Any(source => string.IsNullOrWhiteSpace(source.Item.Name)))
            throw new InvalidOperationException(LibraryResource.TableIsEmpty);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var tables = _sources.Select(source => source.Item.ToSql(Dialect))
            .Where(table => string.IsNullOrWhiteSpace(table) == false).ToList();
        if (tables.Count == 0)
            return;
        builder.Append("From ");
        builder.Append(string.Join(", ", tables));
    }

    /// <inheritdoc />
    public void Clear()
    {
        ReleaseSourceAliases(Register);
        if (Register != null)
            Register.FromType = null;
        Table = null;
        _sources.Clear();
    }

    /// <summary>
    /// 从指定注册器中释放当前根来源已占用的别名。
    /// </summary>
    /// <param name="register">待修改的真实或预检注册器。</param>
    private void ReleaseSourceAliases(IEntityAliasRegister register)
    {
        if (register is not IEntityAliasRegisterLifecycle lifecycle)
            return;
        foreach (var alias in _sources.Select(source => source.Alias)
                     .Where(alias => string.IsNullOrWhiteSpace(alias) == false)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
            lifecycle.ReleaseAlias(alias);
    }

    /// <summary>
    /// 使用单个来源替换当前查询图的全部根来源。
    /// </summary>
    /// <param name="item">新的来源表项。</param>
    /// <param name="entityType">关联实体类型。</param>
    /// <param name="alias">外层查询引用来源时使用的别名。</param>
    /// <param name="projectedMembers">派生来源向外层公开的投影成员。</param>
    private void ReplaceSources(SqlItem item, Type entityType = null, string alias = null,
        IReadOnlyCollection<string> projectedMembers = null)
    {
        Table = item;
        _sources.Clear();
        if (item != null)
            _sources.Add(new TableSource("source_0", item, entityType, alias, projectedMembers));
    }

    /// <summary>
    /// 使用已深复制的根表源替换克隆实例的初始状态。
    /// </summary>
    /// <param name="sources">已深复制的根表源列表。</param>
    private void SetSources(List<TableSource> sources)
    {
        _sources.Clear();
        _sources.AddRange(sources);
        Table = _sources.LastOrDefault()?.Item;
    }

    /// <summary>
    /// 输出Sql。
    /// </summary>
    /// <returns>当前 From 子句的 SQL 文本；没有有效来源时返回 <see langword="null"/>。</returns>
    public string ToSql()
    {
        var result = new StringBuilder();
        AppendTo(result);
        return result.Length == 0 ? null : result.ToString();
    }
}