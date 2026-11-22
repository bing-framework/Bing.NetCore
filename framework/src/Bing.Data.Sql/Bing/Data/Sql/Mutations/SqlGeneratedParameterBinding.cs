using System.Data;
using System.Data.Common;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Mutations;

/// <summary>
/// 生成式写入命令的基础参数绑定实现。
/// </summary>
/// <remarks>
/// 该绑定器只处理已经冻结的 <see cref="SqlParam" /> 元数据，不读取实体属性或调用反射。
/// Provider 定制器由执行层在基础参数创建后继续应用。
/// </remarks>
internal static class SqlGeneratedParameterBinding
{
    /// <summary>
    /// 将冻结参数直接写入数据库命令。
    /// </summary>
    /// <param name="command">目标数据库命令。</param>
    /// <param name="parameters">冻结参数集合。</param>
    internal static void Bind(DbCommand command, IEnumerable<SqlParam> parameters)
    {
        if (command == null || parameters == null)
            return;

        foreach (var parameter in parameters)
        {
            if (parameter == null)
                continue;

            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.Name;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            if (parameter.DbType.HasValue)
                dbParameter.DbType = parameter.DbType.Value;
            if (parameter.Direction.HasValue)
                dbParameter.Direction = parameter.Direction.Value;
            if (parameter.Size.HasValue)
                dbParameter.Size = parameter.Size.Value;
            if (parameter.Precision.HasValue)
                dbParameter.Precision = parameter.Precision.Value;
            if (parameter.Scale.HasValue)
                dbParameter.Scale = parameter.Scale.Value;
            command.Parameters.Add(dbParameter);
        }
    }

}
