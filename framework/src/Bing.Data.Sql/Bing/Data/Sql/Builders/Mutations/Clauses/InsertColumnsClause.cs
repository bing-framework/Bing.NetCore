using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Insert 目标列子句。
/// </summary>
public sealed class InsertColumnsClause : IInsertColumnsClause
{
    /// <summary>
    /// Mutation 子句上下文。
    /// </summary>
    private readonly SqlMutationContext _context;

    /// <summary>
    /// 写入列集合。
    /// </summary>
    private readonly List<string> _columns = new();

    /// <summary>
    /// 初始化一个 <see cref="InsertColumnsClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public InsertColumnsClause(SqlMutationContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public IReadOnlyList<string> Columns => _columns;

    /// <inheritdoc />
    public void Add(string column)
    {
        _context.ValidateOperation(SqlOperationAction.InsertInto);
        if (string.IsNullOrWhiteSpace(column))
            throw new ArgumentException("插入列名不能为空。", nameof(column));
        _context.UseOperation(SqlOperationAction.InsertInto);
        _columns.Add(column);
    }

    /// <inheritdoc />
    public void AddRange(IEnumerable<string> columns)
    {
        _context.ValidateOperation(SqlOperationAction.InsertInto);
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));
        var items = columns.ToList();
        if (items.Count == 0)
            return;
        if (items.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("插入列名不能为空。", "column");
        _columns.AddRange(items);
        _context.UseOperation(SqlOperationAction.InsertInto);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            builder.Append(" (");
            for (var index = 0; index < _columns.Count; index++)
            {
                if (index > 0)
                    builder.Append(", ");
                builder.Append(_context.Dialect.SafeName(_columns[index]));
            }
            builder.Append(')');
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear() => _columns.Clear();

    /// <inheritdoc />
    public IInsertColumnsClause Clone(SqlMutationContext context)
    {
        var result = new InsertColumnsClause(context);
        result._columns.AddRange(_columns);
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("Insert 未指定写入列。");
    }
}