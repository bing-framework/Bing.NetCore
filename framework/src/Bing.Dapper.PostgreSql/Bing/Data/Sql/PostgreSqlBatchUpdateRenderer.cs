using System.Text;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Metadata;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql;

/// <summary>
/// 使用 <c>UPDATE ... FROM (VALUES ...)</c> 语法渲染 PostgreSQL 批量 Update 命令。
/// </summary>
public sealed class PostgreSqlBatchUpdateRenderer : ISqlBatchUpdateRenderer
{
    /// <inheritdoc />
    public string ProviderKey => PostgreSqlSqlProvider.Instance.Key;

    /// <inheritdoc />
    public bool CanRender(SqlBatchUpdateRenderContext context) => context != null &&
        context.Entities.Count > 0 && context.UpdateColumns.Count > 0 && context.Keys.Count > 0;

    /// <inheritdoc />
    public SqlMutationCommand Render(SqlBatchUpdateRenderContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (context.Entities.Count == 0)
            throw new ArgumentException("批量 Update 实体集合不能为空。", nameof(context));
        if (context.UpdateColumns.Count == 0)
            throw new InvalidOperationException($"实体 {context.Mapping.EntityType.Name} 没有可更新列。");
        if (context.Keys.Count == 0)
            throw new InvalidOperationException($"实体 {context.Mapping.EntityType.Name} 没有主键，不能执行优化批量 Update。");
        var parameterManager = context.Provider.ParameterManagerFactory.Create(context.Provider.Dialect);
        var aliases = CreateAliases(context);
        var sql = new StringBuilder(256);
        var table = context.Services.ObjectNameFormatter.Format(context.Mapping.Table, context.Provider.Dialect,
            context.Provider.DatabaseType);
        sql.Append("Update ").Append(table).Append(" As t Set ");
        sql.Append(string.Join(", ", context.UpdateColumns.Select((column, index) =>
            $"{context.Provider.Dialect.SafeName(column.ColumnName)} = v.{aliases[index]}")));
        sql.Append(" From (Values ");
        for (var index = 0; index < context.Entities.Count; index++)
        {
            if (index > 0)
                sql.Append(", ");
            AppendValueRow(sql, context, parameterManager, context.Entities[index]);
        }
        sql.Append(") As v(").Append(string.Join(", ", aliases)).Append(") Where ");
        var conditions = new List<string>();
        var keyOffset = context.UpdateColumns.Count;
        for (var index = 0; index < context.Keys.Count; index++)
            conditions.Add($"t.{context.Provider.Dialect.SafeName(context.Keys[index].ColumnName)} = v.{aliases[keyOffset + index]}");
        var concurrencyOffset = keyOffset + context.Keys.Count;
        for (var index = 0; index < context.ConcurrencyColumns.Count; index++)
            conditions.Add($"t.{context.Provider.Dialect.SafeName(context.ConcurrencyColumns[index].ColumnName)} = v.{aliases[concurrencyOffset + index]}");
        sql.Append(string.Join(" And ", conditions));
        return new SqlMutationCommand(sql.ToString(), ExportParameters(parameterManager));
    }

    /// <summary>
    /// 创建 Values 别名列表。
    /// </summary>
    private static IReadOnlyList<string> CreateAliases(SqlBatchUpdateRenderContext context)
    {
        var aliases = new List<string>(context.UpdateColumns.Count + context.Keys.Count + context.ConcurrencyColumns.Count);
        AddAliases(aliases, context.Provider, "u", context.UpdateColumns.Count);
        AddAliases(aliases, context.Provider, "k", context.Keys.Count);
        AddAliases(aliases, context.Provider, "c", context.ConcurrencyColumns.Count);
        return aliases;
    }

    /// <summary>
    /// 按角色和序号添加与实体物理列名隔离的 Values 别名。
    /// </summary>
    private static void AddAliases(ICollection<string> aliases, ISqlProvider provider, string role, int count)
    {
        for (var index = 0; index < count; index++)
            aliases.Add(provider.Dialect.SafeName($"__mutation_{role}_{index}"));
    }

    /// <summary>
    /// 追加单个实体的 Values 参数行。
    /// </summary>
    private static void AppendValueRow(StringBuilder sql, SqlBatchUpdateRenderContext context,
        IParameterManager parameterManager, object entity)
    {
        sql.Append('(');
        var first = true;
        foreach (var column in context.UpdateColumns)
            AppendParameter(sql, context, parameterManager, entity, column, false, ref first);
        foreach (var column in context.Keys)
            AppendParameter(sql, context, parameterManager, entity, column, true, ref first);
        foreach (var column in context.ConcurrencyColumns)
            AppendParameter(sql, context, parameterManager, entity, column, true, ref first, true);
        sql.Append(')');
    }

    /// <summary>
    /// 创建并追加单个参数。
    /// </summary>
    private static void AppendParameter(StringBuilder sql, SqlBatchUpdateRenderContext context,
        IParameterManager parameterManager, object source, ColumnMappingMetadata column, bool rejectNull, ref bool first,
        bool concurrency = false)
    {
        var value = concurrency ? context.GetConcurrencyValue(source, column) : context.GetValue(source, column);
        if (rejectNull && value == null)
            throw new InvalidOperationException($"实体 {context.Mapping.EntityType.Name} 的条件列 {column.PropertyName} 不能为空。");
        var parameter = context.Services.ParameterFactory.Create(parameterManager.GenerateName(), value, column,
            context.DatabaseContext, context.Mapping.EntityType, SqlParameterSource.SqlBuilder);
        AddParameter(parameterManager, parameter);
        if (first == false)
            sql.Append(", ");
        sql.Append(context.Provider.Dialect.GetParamName(parameter.Name));
        first = false;
    }

    /// <summary>
    /// 保存包含元数据的参数。
    /// </summary>
    private static void AddParameter(IParameterManager parameterManager, SqlParam parameter)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            advancedParameterManager.Add(parameter);
        else
            parameterManager.Add(parameter.Name, parameter.Value);
    }

    /// <summary>
    /// 导出可执行参数快照。
    /// </summary>
    private static IReadOnlyCollection<SqlParam> ExportParameters(IParameterManager parameterManager)
    {
        if (parameterManager is IAdvancedParameterManager advancedParameterManager)
            return advancedParameterManager.GetSqlParams().Values.ToArray();
        return parameterManager.GetParams().Select(item => new SqlParam(item.Key, item.Value)).ToArray();
    }
}