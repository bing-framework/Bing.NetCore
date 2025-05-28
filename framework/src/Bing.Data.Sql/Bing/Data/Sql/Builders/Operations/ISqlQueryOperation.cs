namespace Bing.Data.Sql.Builders.Operations;

/// <summary>
/// Sql查询操作
/// </summary>
public interface ISqlQueryOperation : ISelect, IFrom, IJoin, IWhere, IGroupBy, IOrderBy, IUnion, ICte, ISqlParameter
{
}
