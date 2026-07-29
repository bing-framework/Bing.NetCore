using System.Text;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Insert Values 子句。
/// </summary>
public sealed class ValuesClause : IValuesClause
{
    /// <summary>
    /// Mutation 子句上下文。
    /// </summary>
    private readonly SqlMutationContext _context;

    /// <summary>
    /// 已分配的参数名行集合。
    /// </summary>
    private readonly List<IReadOnlyList<string>> _rows = new();

    /// <summary>
    /// 初始化一个 <see cref="ValuesClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public ValuesClause(SqlMutationContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public int RowCount => _rows.Count;

    /// <inheritdoc />
    public int ColumnCount => _rows.Count == 0 ? 0 : _rows[0].Count;

    /// <inheritdoc />
    public void AddRow(IReadOnlyList<object> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (_rows.Count > 0 && values.Count != ColumnCount)
            throw new InvalidOperationException("Insert Values 行列数量不一致。");
        var names = new List<string>(values.Count);
        foreach (var value in values)
        {
            var name = _context.ParameterManager.GenerateName();
            _context.ParameterManager.Add(name, value);
            names.Add(name);
        }
        _rows.Add(names);
    }

    /// <inheritdoc />
    public void AddRow(IReadOnlyList<SqlParam> parameters)
    {
        if (parameters == null)
            throw new ArgumentNullException(nameof(parameters));
        if (_rows.Count > 0 && parameters.Count != ColumnCount)
            throw new InvalidOperationException("Insert Values 行列数量不一致。");
        var names = new List<string>(parameters.Count);
        foreach (var parameter in parameters)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                throw new ArgumentException("Insert 参数名称不能为空。", nameof(parameters));
            if (_context.ParameterManager is IAdvancedParameterManager advancedManager)
                advancedManager.Add(parameter);
            else
                _context.ParameterManager.Add(parameter.Name, parameter.Value);
            names.Add(parameter.Name);
        }
        _rows.Add(names);
    }

    /// <inheritdoc />
    public void AddRows(IEnumerable<IReadOnlyList<object>> rows)
    {
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));
        foreach (var row in rows)
            AddRow(row);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Append(" Values ");
        for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
        {
            if (rowIndex > 0)
                builder.Append(", ");
            builder.Append('(');
            var row = _rows[rowIndex];
            for (var columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                if (columnIndex > 0)
                    builder.Append(", ");
                builder.Append(_context.Dialect.GetParamName(row[columnIndex]));
            }
            builder.Append(')');
        }
    }

    /// <inheritdoc />
    public void Clear() => _rows.Clear();

    /// <inheritdoc />
    public IValuesClause Clone(SqlMutationContext context)
    {
        var result = new ValuesClause(context);
        foreach (var row in _rows)
            result._rows.Add(row.ToArray());
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (_rows.Count == 0)
            throw new InvalidOperationException("Insert Values 不能为空。");
        if (_rows.Any(row => row.Count != ColumnCount))
            throw new InvalidOperationException("Insert Values 行列数量不一致。");
        if (_rows.Count > 1 && context?.Capabilities.SupportsMultiRowValues == false)
            throw new NotSupportedException($"Provider {context.Provider.Key} 不支持多行 Values。");
    }
}