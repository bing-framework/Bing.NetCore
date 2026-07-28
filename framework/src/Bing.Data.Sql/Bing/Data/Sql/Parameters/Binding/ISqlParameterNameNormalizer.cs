namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数名称规范化器
/// </summary>
public interface ISqlParameterNameNormalizer
{
    /// <summary>
    /// 将参数名称转换为不含 Provider 前缀的标准名称
    /// </summary>
    /// <param name="name">原始参数名称</param>
    /// <returns>标准参数名称</returns>
    string Normalize(string name);
}