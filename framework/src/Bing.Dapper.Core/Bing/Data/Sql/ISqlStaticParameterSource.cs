namespace Bing.Data.Sql;

/// <summary>无需 Dapper 动态代码即可写入命令的已准备参数。</summary>
internal interface ISqlStaticParameterSource
{
    void BindTo(IDbCommand command);
}
