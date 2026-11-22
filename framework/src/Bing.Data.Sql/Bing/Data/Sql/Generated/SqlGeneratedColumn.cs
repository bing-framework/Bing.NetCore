using System.ComponentModel;
using Bing.Data.Sql.Builders;

namespace Bing.Data.Sql;

/// <summary>
/// 编译期 SQL 查询的逻辑列描述。
/// </summary>
/// <remarks>编译期生成的逻辑成员描述；不包含 Provider SQL。</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SqlGeneratedColumn
{
    /// <summary>
    /// 初始化一个 <see cref="SqlGeneratedColumn" /> 类型的实例。
    /// </summary>
    /// <param name="sourceIndex">数据源中的列序号。</param>
    /// <param name="memberName">当前成员的名称。</param>
    /// <param name="resultAlias">查询结果中使用的别名。</param>
    /// <param name="resultType">查询结果的 CLR 类型。</param>
    /// <param name="function">用于生成查询结果的聚合函数。</param>
    /// <param name="distinct">指示是否仅保留不同的值。</param>
    public SqlGeneratedColumn(int sourceIndex, string memberName, string resultAlias, Type resultType,
        SqlAggregateFunction? function = null, bool distinct = false)
    {
        SourceIndex = sourceIndex; MemberName = memberName; ResultAlias = resultAlias;
        ResultType = resultType; Function = function; Distinct = distinct;
    }
    /// <summary>
    /// 获取投影列所属查询源的序号。
    /// </summary>
    public int SourceIndex { get; }
    /// <summary>
    /// 获取CLR 成员名称。
    /// </summary>
    public string MemberName { get; }
    /// <summary>
    /// 获取投影结果列的别名。
    /// </summary>
    public string ResultAlias { get; }
    /// <summary>
    /// 获取管线或投影结果的 CLR 类型。
    /// </summary>
    public Type ResultType { get; }
    /// <summary>
    /// 获取投影或比较使用的 SQL 聚合函数。
    /// </summary>
    public SqlAggregateFunction? Function { get; }
    /// <summary>
    /// 获取聚合是否去除重复值。
    /// </summary>
    public bool Distinct { get; }
}
