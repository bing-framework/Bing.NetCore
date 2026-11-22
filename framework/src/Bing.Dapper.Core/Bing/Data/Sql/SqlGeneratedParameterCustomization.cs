using System.Data;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 生成式参数源使用的 Provider 定制器适配逻辑。
/// </summary>
internal static class SqlGeneratedParameterCustomization
{
    /// <summary>
    /// 对生成绑定器创建的数据库参数应用与通用绑定器一致的定制顺序。
    /// </summary>
    /// <param name="command">当前数据库命令。</param>
    /// <param name="parameters">已冻结的 SQL 参数。</param>
    /// <param name="customizers">当前执行上下文登记的参数定制器。</param>
    internal static void Apply(IDbCommand command, IEnumerable<SqlParam> parameters,
        IReadOnlyCollection<ISqlDbParameterCustomizer> customizers)
    {
        if (command == null || parameters == null || customizers == null || customizers.Count == 0)
            return;
        foreach (var parameter in parameters)
        {
            if (parameter == null)
                continue;
            var dbParameter = FindParameter(command, parameter.Name);
            if (dbParameter == null)
                continue;
            var databaseType = parameter.DatabaseType ?? default(DatabaseType);
            foreach (var customizer in customizers)
            {
                if (customizer != null && customizer is not ISqlTypeHandlerParameterAdapter &&
                    customizer.CanHandle(databaseType))
                {
                    customizer.Configure(dbParameter, parameter);
                    break;
                }
            }
            foreach (var customizer in customizers)
            {
                if (customizer is ISqlTypeHandlerParameterAdapter && customizer.CanHandle(databaseType))
                    customizer.Configure(dbParameter, parameter);
            }
        }
    }

    /// <summary>
    /// 按参数名查找生成绑定器创建的数据库参数。
    /// </summary>
    private static IDbDataParameter FindParameter(IDbCommand command, string name)
    {
        var normalizedName = NormalizeName(name);
        foreach (IDataParameter parameter in command.Parameters)
        {
            if (string.Equals(NormalizeName(parameter.ParameterName), normalizedName,
                    StringComparison.OrdinalIgnoreCase))
                return parameter as IDbDataParameter;
        }
        return null;
    }

    /// <summary>
    /// 统一 ADO.NET 参数前缀。
    /// </summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        name = name.Trim();
        return name[0] is '@' or ':' or '?' or '$' ? name.Substring(1) : name;
    }
}
