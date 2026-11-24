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

    /// <summary>完整生成子句的选择器；追加普通列后失效。</summary>
    private Func<string, SqlExecutionKind, string> _generatedSql;

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
        _generatedSql = null;
    }

    /// <summary>尝试采用生成器提供的完整 Returning 子句，扩展服务或已有投影保留动态路径。</summary>
    /// <param name="columns">已核对最终映射的不可变返回列。</param>
    /// <param name="sqlFactory">按 Provider 和操作类型选择完整 SQL 的静态工厂。</param>
    /// <returns>成功采用生成 SQL 时返回 true；调用方在 false 时继续使用 AddRange。</returns>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public bool TryAddGenerated(IReadOnlyList<SqlReturningColumn> columns,
        Func<string, SqlExecutionKind, string> sqlFactory)
    {
        if (sqlFactory == null)
            throw new ArgumentNullException(nameof(sqlFactory));
        if (IsEmpty == false || SqlMutationPlan.SupportsGeneratedSql(_context.Provider, _context.Services) == false)
            return false;
        AddRange(columns);
        _generatedSql = sqlFactory;
        return true;
    }

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        var startIndex = builder.Length;
        try
        {
            var generated = _generatedSql?.Invoke(_context.Provider.Key, _executionKind);
            if (generated != null)
            {
                builder.Append(generated);
                return;
            }
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
                column.AppendTo(builder, _context.Dialect, qualifier);
            }
        }
        catch
        {
            builder.Remove(startIndex, builder.Length - startIndex);
            throw;
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _columns.Clear();
        _generatedSql = null;
    }

    /// <inheritdoc />
    public IReturningClause Clone(SqlMutationContext context)
    {
        var result = new ReturningClause(context);
        if (_columns.Count > 0)
        {
            // 列值不可变，仅复制集合，Clear/Add 和验证状态仍由各子句独立维护。
            result.AddRange(_columns);
            if (_generatedSql != null && SqlMutationPlan.SupportsGeneratedSql(context.Provider, context.Services))
                result._generatedSql = _generatedSql;
        }
        return result;
    }

    /// <inheritdoc />
    public void Validate(SqlValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (IsEmpty)
            throw new InvalidOperationException("Returning 未指定返回列。");
        ValidateProvider(context);
        _executionKind = context.ExecutionKind;
    }

    /// <summary>生成式命令与 Returning 子句共用构建期能力校验。</summary>
    internal static void ValidateProvider(SqlValidationContext context)
    {
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
    }
}
