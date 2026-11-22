using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders.Conditions;

/// <summary>
/// 克隆时重新绑定上下文，保留逻辑列而非已格式化 SQL。
/// </summary>
internal interface ISqlBoundCondition : ICondition
{
    /// <summary>
    /// 创建绑定指定 SQL 上下文的条件。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <returns>绑定新上下文的条件。</returns>
    ICondition Rebind(SqlClauseContext context);
    /// <summary>
    /// 获取条件引用的逻辑投影列。
    /// </summary>
    IEnumerable<SqlProjectionColumn> Columns { get; }
}

/// <summary>
/// 捕获查询条件并按子句上下文重新绑定来源的工具。
/// </summary>
internal static class SqlConditionSnapshot
{
    /// <summary>
    /// 获取绑定条件引用的投影列。
    /// </summary>
    /// <param name="condition">要检查的条件。</param>
    /// <returns>绑定条件的投影列；条件未绑定列时返回空集合。</returns>
    internal static IEnumerable<SqlProjectionColumn> Columns(ICondition condition) =>
        condition is ISqlBoundCondition bound ? bound.Columns : Enumerable.Empty<SqlProjectionColumn>();
    /// <summary>
    /// 捕获条件并保留其可重绑定能力。
    /// </summary>
    /// <param name="condition">要捕获的条件。</param>
    /// <returns>null、原绑定条件，或包装普通条件的 SQL 条件对象。</returns>
    internal static ICondition Capture(ICondition condition) => condition == null ? null : condition is ISqlBoundCondition
        ? condition : new SqlCondition(condition.GetCondition());
    /// <summary>
    /// 将条件重新绑定到指定 SQL 子句上下文。
    /// </summary>
    /// <param name="condition">要重新绑定的条件。</param>
    /// <param name="context">目标 SQL 子句上下文。</param>
    /// <returns>重绑定后的条件；普通条件通过 Capture 转换。</returns>
    internal static ICondition Rebind(ICondition condition, SqlClauseContext context) =>
        condition is ISqlBoundCondition bound ? bound.Rebind(context) : Capture(condition);
}

/// <summary>
/// 已参数化比较值与列来源的绑定。
/// </summary>
/// <remarks>已参数化比较；来源别名和列名在当前查询快照中解析。</remarks>
/// <param name="Column">比较值绑定的逻辑投影列。</param>
/// <param name="Source">比较列所属的查询表来源。</param>
/// <param name="Value">已经参数化的值文本。</param>
internal sealed record SqlBoundOperand(SqlProjectionColumn Column, TableSource Source, string Value)
{
    /// <summary>
    /// 渲染绑定列或参数值对应的条件操作数。
    /// </summary>
    /// <param name="context">提供数据库方言和别名信息的 SQL 子句上下文。</param>
    /// <returns>列对应的 SQL 文本；没有列时返回操作数值。</returns>
    internal string Render(SqlClauseContext context) => Column?.ToColumn(context, Source).ToSql(context.Dialect, context.AliasRegister) ?? Value;
}

/// <summary>
/// 保存绑定的来源列并创建可重绑定的 SQL 条件。
/// </summary>
internal sealed class SqlBoundCondition : ISqlBoundCondition
{
    /// <summary>
    /// 保存条件绑定时使用的 SQL 子句上下文。
    /// </summary>
    private readonly SqlClauseContext _context;
    /// <summary>
    /// 保存二元条件绑定的左操作数。
    /// </summary>
    private readonly SqlBoundOperand _left;
    /// <summary>
    /// 保存二元条件绑定的右操作数。
    /// </summary>
    private readonly SqlBoundOperand _right;
    /// <summary>
    /// 保存二元条件的比较运算符。
    /// </summary>
    private readonly Operator _operator;
    /// <summary>
    /// 初始化一个 <see cref="SqlBoundCondition" /> 类型的实例。
    /// </summary>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    /// <param name="left">当前 SQL 条件左侧的绑定操作数。</param>
    /// <param name="right">当前 SQL 条件右侧的绑定操作数。</param>
    /// <param name="operator">当前 SQL 条件使用的运算符。</param>
    internal SqlBoundCondition(SqlClauseContext context, SqlBoundOperand left, SqlBoundOperand right, Operator @operator)
    {
        _context = context;
        _left = left;
        _right = right;
        _operator = @operator;
    }
    /// <inheritdoc />
    public IEnumerable<SqlProjectionColumn> Columns => new[] { _left.Column, _right.Column }.Where(column => column != null);
    /// <inheritdoc />
    public string GetCondition() => SqlConditionFactory.Create(_left.Render(_context), _right.Render(_context), _operator).GetCondition();
    /// <inheritdoc />
    public ICondition Rebind(SqlClauseContext context) => new SqlBoundCondition(context, _left, _right, _operator);
}

/// <summary>保留取反条件中的逻辑列，以便克隆后重新绑定查询上下文。</summary>
internal sealed class SqlNegatedCondition : ISqlBoundCondition
{
    private readonly ICondition _operand;

    internal SqlNegatedCondition(ICondition operand) => _operand = SqlConditionSnapshot.Capture(operand);

    IEnumerable<SqlProjectionColumn> ISqlBoundCondition.Columns => SqlConditionSnapshot.Columns(_operand);

    ICondition ISqlBoundCondition.Rebind(SqlClauseContext context) =>
        new SqlNegatedCondition(SqlConditionSnapshot.Rebind(_operand, context));

    public string GetCondition() => $"Not ({_operand.GetCondition()})";
}

/// <summary>
/// 保留集合参数与逻辑列绑定的 In 条件。
/// </summary>
/// <remarks>
/// 保留集合参数名称与逻辑列，克隆或别名推断不会改变参数数量。
/// </remarks>
internal sealed class SqlBoundInCondition : ISqlBoundCondition
{
    /// <summary>
    /// 保存 IN 条件绑定时使用的 SQL 子句上下文。
    /// </summary>
    private readonly SqlClauseContext _context;
    /// <summary>
    /// 保存 IN 条件比较的列操作数。
    /// </summary>
    private readonly SqlBoundOperand _column;
    /// <summary>
    /// 保存 IN 条件展开后的参数名数组。
    /// </summary>
    private readonly string[] _parameters;
    /// <summary>
    /// 指示 IN 条件是否取反为 NOT IN。
    /// </summary>
    private readonly bool _notIn;
    /// <summary>
    /// 初始化一个 <see cref="SqlBoundInCondition" /> 类型的实例。
    /// </summary>
    /// <param name="context">当前操作关联的上下文或工作单元。</param>
    /// <param name="column">当前条件对应的数据库列或列值。</param>
    /// <param name="parameters">当前调用或 SQL 条件使用的参数集合。</param>
    /// <param name="notIn">指示当前 SQL 条件是否使用 NOT IN 运算。</param>
    internal SqlBoundInCondition(SqlClauseContext context, SqlBoundOperand column, IEnumerable<string> parameters, bool notIn)
    { _context = context; _column = column; _parameters = parameters.ToArray(); _notIn = notIn; }
    /// <inheritdoc />
    public IEnumerable<SqlProjectionColumn> Columns => _column.Column == null ? Enumerable.Empty<SqlProjectionColumn>() : new[] { _column.Column };
    /// <inheritdoc />
    public string GetCondition() => _notIn ? new NotInCondition(_column.Render(_context), _parameters).GetCondition() : new InCondition(_column.Render(_context), _parameters).GetCondition();
    /// <inheritdoc />
    public ICondition Rebind(SqlClauseContext context) => new SqlBoundInCondition(context, _column, _parameters, _notIn);
}
