namespace Bing.Dapper.Tests.Infrastructure;

/// <summary>
/// MySQL 带点物理表 Company 测试实体。
/// </summary>
internal sealed class MySqlDottedCompany
{
    /// <summary>
    /// 公司标识。
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// 商户标识。
    /// </summary>
    public Guid? MerchantId { get; set; }

    /// <summary>
    /// 公司名称。
    /// </summary>
    public string Name { get; set; }
}

/// <summary>
/// MySQL 带点物理表 Merchant 测试实体。
/// </summary>
internal sealed class MySqlDottedMerchant
{
    /// <summary>
    /// 商户标识。
    /// </summary>
    public Guid MerchantId { get; set; }

    /// <summary>
    /// 商户名称。
    /// </summary>
    public string Name { get; set; }
}