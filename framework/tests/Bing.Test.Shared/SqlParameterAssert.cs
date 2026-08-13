using System.Data;
using Bing.Data.Sql.Builders.Params;
using Xunit;

namespace Bing.Test.Shared;

/// <summary>
/// SQL 参数断言。
/// </summary>
public static class SqlParameterAssert
{
    /// <summary>
    /// 断言参数的名称、值、类型和方向。
    /// </summary>
    /// <param name="parameters">实际参数集合。</param>
    /// <param name="name">参数名称。</param>
    /// <param name="value">期望参数值。</param>
    /// <param name="dbType">期望数据库类型。</param>
    /// <param name="direction">期望参数方向。</param>
    public static void Equal(IReadOnlyCollection<SqlParam> parameters, string name, object value,
        DbType? dbType = null, ParameterDirection? direction = null)
    {
        var parameter = parameters?.SingleOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        Assert.True(parameter != null, $"未找到参数 {name}。实际参数: {Describe(parameters)}");
        Assert.Equal(value, parameter.Value);
        if (dbType.HasValue)
            Assert.Equal(dbType, parameter.DbType);
        if (direction.HasValue)
            Assert.Equal(direction, parameter.Direction);
    }

    /// <summary>
    /// 将参数格式化为失败诊断文本。
    /// </summary>
    /// <param name="parameter">参数。</param>
    /// <returns>可读参数文本。</returns>
    public static string Describe(SqlParam parameter) => parameter == null
        ? "<null>"
        : $"{parameter.Name}: Value={parameter.Value ?? "<null>"}, DbType={parameter.DbType?.ToString() ?? "<null>"}, Direction={parameter.Direction?.ToString() ?? "<null>"}";

    /// <summary>
    /// 将参数集合格式化为失败诊断文本。
    /// </summary>
    /// <param name="parameters">参数集合。</param>
    /// <returns>可读参数集合文本。</returns>
    public static string Describe(IEnumerable<SqlParam> parameters) => parameters == null
        ? "<null>"
        : string.Join("; ", parameters.Select(Describe));
}