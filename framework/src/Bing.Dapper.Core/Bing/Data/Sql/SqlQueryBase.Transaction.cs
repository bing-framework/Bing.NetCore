using System.Runtime.ExceptionServices;
using System.Collections;
using Bing.Data.Filters;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations.Accessors;
using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Configs;
using Bing.Data.Sql.Diagnostics;
using Bing.Data.Sql.Metadata;
using Bing.Data.Enums;
using Bing.Helpers;
using Bing.Text;
using Bing.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Bing.Data.Sql;

public abstract partial class SqlExecutionBase
{
    /// <summary>
    /// 确保当前 Provider 支持存储过程命令。
    /// </summary>
    private protected void EnsureStoredProceduresSupported()
    {
        EnsureWritableDataSource();
        var profile = GetRequiredProviderProfile();
        if (profile.Procedure.SupportsStoredProcedures)
            return;
        throw SqlCapabilityFailure.Create(profile.Procedure.StoredProceduresFailureReason ??
            SqlCapabilityFailureReason.DatabaseUnsupported, "StoredProcedures",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持存储过程命令。");
    }

    /// <summary>
    /// 确保当前 Provider 支持调用方请求的输出参数。
    /// </summary>
    /// <param name="parameters">调用方声明的参数。</param>
    /// <param name="storedProcedure">当前命令是否为存储过程。</param>
    private protected void EnsureOutputParametersSupported(object parameters, bool storedProcedure = true)
    {
        var profile = GetRequiredProviderProfile();
        if (HasOutputParameters(parameters) == false || profile.Procedure.SupportsOutputParameters)
            return;
        throw SqlCapabilityFailure.Create(profile.Procedure.OutputParametersFailureReason ??
            SqlCapabilityFailureReason.ProviderImplementationGap, "OutputParameters",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持{(storedProcedure ? "存储过程" : "SQL 文本")}输出参数。");
    }

    /// <summary>
    /// 判断参数源是否声明输出方向。
    /// </summary>
    /// <param name="parameters">待检查的参数源。</param>
    /// <returns>包含 Output、InputOutput 或 ReturnValue 参数时返回 <see langword="true"/>。</returns>
    private static bool HasOutputParameters(object parameters)
    {
        if (SqlRuntimeFeatures.ReflectionFallbackEnabled && parameters is global::Dapper.DynamicParameters dynamicParameters)
            return DynamicParametersOutputAccessor.HasOutputParameters(dynamicParameters);
        if (parameters is SqlParam parameter)
            return IsOutputDirection(parameter.Direction);
        if (parameters is IEnumerable<SqlParam> sqlParameters)
            return sqlParameters.Any(item => item != null && IsOutputDirection(item.Direction));
        if (parameters is ISqlParameterMap parameterMap)
            return parameterMap.GetItems().Any(item => item != null && IsOutputDirection(item.Direction));
        return false;
    }

    /// <summary>
    /// 判断参数方向是否需要在过程完成后读取结果。
    /// </summary>
    /// <param name="direction">参数方向。</param>
    /// <returns>需要输出访问器时返回 <see langword="true"/>。</returns>
    private static bool IsOutputDirection(ParameterDirection? direction) => direction is ParameterDirection.Output or
        ParameterDirection.InputOutput or ParameterDirection.ReturnValue;

    /// <summary>
    /// 确保当前 Provider 支持请求的异步取消。
    /// </summary>
    /// <param name="cancellationToken">调用方传入的取消令牌。</param>
    private protected void EnsureCancellationSupported(CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled == false)
            return;
        if (cancellationToken.IsCancellationRequested)
            MarkPreCancelledUnitOfWorkExecution();
        cancellationToken.ThrowIfCancellationRequested();
        var profile = GetRequiredProviderProfile();
        if (profile.Execution.SupportsCancellation)
            return;
        throw SqlCapabilityFailure.Create(profile.Execution.CancellationFailureReason ??
            SqlCapabilityFailureReason.ProviderImplementationGap, "Cancellation",
            GetCurrentProviderKey(),
            $"Provider {GetCurrentProvider().Key} 不支持异步命令取消。");
    }

    /// <summary>
    /// 确保当前数据源允许执行结构化写入或事务操作。
    /// </summary>
    /// <param name="dataSource">当前执行数据源。</param>
    private protected void EnsureWritableDataSource(SqlDataSourceDescriptor dataSource = null)
    {
        dataSource ??= Options.GetDatabaseContext()?.DataSource;
        if (dataSource?.IsReadOnly != true)
            return;
        var dbKey = dataSource.Key ?? Options.GetDatabaseContext()?.DbKey ?? "<default>";
        throw SqlCapabilityFailure.Create(SqlCapabilityFailureReason.DatabaseUnsupported, "WritableDataSource", dbKey,
            $"数据源 {dbKey} 是只读数据源，不支持写入或事务操作。");
    }

}
