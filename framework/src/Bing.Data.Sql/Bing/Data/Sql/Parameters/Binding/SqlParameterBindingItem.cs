using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数绑定项
/// </summary>
public sealed class SqlParameterBindingItem
{
    /// <summary>
    /// 获取或设置标准参数名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置原始参数值
    /// </summary>
    public object OriginalValue { get; set; }

    /// <summary>
    /// 获取或设置最终参数值
    /// </summary>
    public object Value { get; set; }

    /// <summary>
    /// 获取或设置是否存在参数值
    /// </summary>
    public bool HasValue { get; set; }

    /// <summary>
    /// 获取或设置是否显式空值
    /// </summary>
    public bool IsExplicitNull { get; set; }

    /// <summary>
    /// 获取或设置参数元数据
    /// </summary>
    public SqlParam Metadata { get; set; }
}