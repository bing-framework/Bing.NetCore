namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 带点物理表 Company 测试实体。
/// </summary>
internal sealed class MySqlDottedCompany
{
    /// <summary>
    /// 获取或设置公司标识。
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// 获取或设置商户标识。
    /// </summary>
    public Guid? MerchantId { get; set; }

    /// <summary>
    /// 获取或设置公司名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// MySQL 带点物理表 Merchant 测试实体。
/// </summary>
internal sealed class MySqlDottedMerchant
{
    /// <summary>
    /// 获取或设置商户标识。
    /// </summary>
    public Guid MerchantId { get; set; }

    /// <summary>
    /// 获取或设置商户名称。
    /// </summary>
    public string Name { get; set; }
}