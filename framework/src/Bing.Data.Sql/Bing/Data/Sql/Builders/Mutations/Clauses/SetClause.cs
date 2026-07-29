using System.Text;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Update Set 子句。
/// </summary>
public sealed class SetClause : ISetClause
{
    /// <summary>
    /// Mutation 子句上下文。
    /// </summary>
    private readonly SqlMutationContext _context;

    /// <summary>
    /// 列与参数名集合。
    /// </summary>
    private readonly List<KeyValuePair<string, string>> _items = new();

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
        var name = _context.ParameterManager.GenerateName();
        _context.ParameterManager.Add(name, value);
        _items.Add(new KeyValuePair<string, string>(column, name));
    }

    /// <inheritdoc />
    public void Set(string column, SqlParam parameter)
    {
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentException("更新列名不能为空。", nameof(column));
        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
            throw new ArgumentException("更新参数名称不能为空。", nameof(parameter));
        if (_context.ParameterManager is IAdvancedParameterManager advancedManager)
            advancedManager.Add(parameter);
        else
            _context.ParameterManager.Add(parameter.Name, parameter.Value);
        _items.Add(new KeyValuePair<string, string>(column, parameter.Name));
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
            builder.Append(_context.Dialect.SafeName(item.Key));
            builder.Append(" = ");
            builder.Append(_context.Dialect.GetParamName(item.Value));
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
}