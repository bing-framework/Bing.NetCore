using System.Text;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations.Clauses;

/// <summary>
/// 默认 Mutation Returning 子句。
/// </summary>
public sealed class ReturningClause : IReturningClause
{
    /// <summary>
    /// 当前 Mutation 的 Provider、方言和操作状态上下文。
    /// </summary>
    private readonly SqlMutationContext _context;

    /// <summary>
    /// 按调用顺序保存的返回投影列。
    /// </summary>
    private readonly List<SqlReturningColumn> _columns = new();

    /// <summary>
    /// 验证时固定的执行类型，用于选择 Provider 特定关键字和列限定符。
    /// </summary>
    private SqlExecutionKind _executionKind;

    /// <summary>
    /// 初始化一个 <see cref="ReturningClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public ReturningClause(SqlMutationContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    /// <inheritdoc />
    public bool IsEmpty => _columns.Count == 0;

    /// <inheritdoc />
    public void AddRange(IReadOnlyList<SqlReturningColumn> columns)
    {
        if (columns == null)
            throw new ArgumentNullException(nameof(columns));
        if (columns.Count == 0)
            throw new ArgumentException("Returning 必须包含至少一个返回列。", nameof(columns));
        _context.UseOperation(SqlOperationAction.Returning);
        _columns.AddRange(columns);
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            var dialect = _context.Provider as ISqlReturningDialect;
            var keyword = dialect?.GetKeyword(_executionKind) ?? "Returning";
            if (string.IsNullOrWhiteSpace(keyword))
                throw new InvalidOperationException($"Provider {_context.Provider.Key} 返回了无效的 Returning 关键字。");
            builder.Append(' ').Append(keyword).Append(' ');
            for (var index = 0; index < _columns.Count; index++)
            {
                if (index > 0)
                    builder.Append(", ");
                var column = _columns[index];
                var qualifier = dialect?.GetQualifier(_executionKind, column.Qualifier) ?? column.Qualifier;
                if (string.IsNullOrWhiteSpace(qualifier) == false)
                    builder.Append(_context.Dialect.SafeName(qualifier)).Append('.');
                builder.Append(_context.Dialect.SafeName(column.Column));
                if (string.IsNullOrWhiteSpace(column.Alias) == false)
                    builder.Append(" As ").Append(_context.Dialect.SafeName(column.Alias));
            }
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
    public IReturningClause Clone(SqlMutationContext context)
    {
        var result = new ReturningClause(context);
        if (_columns.Count > 0)
            result.AddRange(_columns.Select(column => new SqlReturningColumn(column.Column, column.Qualifier,
                column.Alias)).ToArray());
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (IsEmpty)
            throw new InvalidOperationException("Returning 未指定返回列。");
        if (context.IsProfileDeclared == false)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMissing, "Returning",
                context.Provider.Key, $"Provider {context.Provider.Key} 不支持 Returning。");
        if (SqlProviderCapabilityResolver.HasCompleteProfile(context.Provider) == false)
            throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.ProviderProfileMismatch, "Returning",
                context.Provider.Key, $"Provider {context.Provider.Key} 的 Mutation 能力 Profile 不完整。[ProfileMismatch]");
        if (context.Profile.Mutation.SupportsReturning == false)
            throw SqlCapabilityFailure.Create(context.Profile.Mutation.ReturningFailureReason ??
                SqlCapabilityFailureReason.ProviderImplementationGap, "Returning",
                context.Provider.Key, $"Provider {context.Provider.Key} 不支持 Returning。");
        _executionKind = context.ExecutionKind;
    }
}