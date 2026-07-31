using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Update Set 子句。
/// </summary>
public sealed class SetClause : ISetClause, IColumnSetClause
{
    /// <summary>
    /// Mutation 子句上下文。
    /// </summary>
    private readonly SqlMutationContext _context;

    /// <summary>
    /// 列与参数名集合。
    /// </summary>
    private readonly List<SetItem> _items = new();

    /// <summary>
    /// 初始化一个 <see cref="SetClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public SetClause(SqlMutationContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <inheritdoc />
    public void Set(string column, object value)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentException("更新列名不能为空。", nameof(column));
        _context.UseOperation(SqlOperationAction.Set);
        var name = _context.ParameterManager.GenerateName();
        _context.ParameterManager.Add(name, value);
        _items.Add(new SetItem(column, name, true));
    }

    /// <inheritdoc />
    public void Set(string column, SqlParam parameter)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentException("更新列名不能为空。", nameof(column));
        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            throw new ArgumentException("更新参数名称不能为空。", nameof(parameter));
        _context.UseOperation(SqlOperationAction.Set);
        if (_context.ParameterManager is IAdvancedParameterManager advancedManager)
            advancedManager.Add(parameter);
        else
            _context.ParameterManager.Add(parameter.Name, parameter.Value);
        _items.Add(new SetItem(column, parameter.Name, true));
    }

    /// <inheritdoc />
    public void SetFrom(string targetColumn, string sourceAlias, string sourceColumn)
    {
        if (string.IsNullOrWhiteSpace(targetColumn))
            throw new ArgumentException("更新列名不能为空。", nameof(targetColumn));
        if (string.IsNullOrWhiteSpace(sourceAlias))
            throw new ArgumentException("Update From 来源表别名不能为空。", nameof(sourceAlias));
        if (string.IsNullOrWhiteSpace(sourceColumn))
            throw new ArgumentException("Update From 来源列名不能为空。", nameof(sourceColumn));
        _context.UseOperation(SqlOperationAction.Set);
        _items.Add(new SetItem(targetColumn,
            $"{_context.Dialect.SafeName(sourceAlias)}.{_context.Dialect.SafeName(sourceColumn)}", false));
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Append(" Set ");
        for (var index = 0; index < _items.Count; index++)
        {
            if (index > 0)
                builder.Append(", ");
            var item = _items[index];
            builder.Append(_context.Dialect.SafeName(item.Column));
            builder.Append(" = ");
            builder.Append(item.IsParameter ? _context.Dialect.GetParamName(item.Value) : item.Value);
        }
    }

    /// <inheritdoc />
    public void Clear() => _items.Clear();

    /// <inheritdoc />
    public ISetClause Clone(SqlMutationContext context)
    {
        var result = new SetClause(context);
        result._items.AddRange(_items);
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Update 未指定 Set 赋值。");
    }

    private sealed class SetItem
    {
        public SetItem(string column, string value, bool isParameter)
        {
            Column = column;
            Value = value;
            IsParameter = isParameter;
        }

        public string Column { get; }

        public string Value { get; }

        public bool IsParameter { get; }
    }
}