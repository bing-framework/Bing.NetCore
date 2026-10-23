namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

/// <summary>
/// orders.db 中由 Dapper 和 EF Core 共同写入的演示记录。
/// </summary>
public sealed class OrderRecord
{
    public string Id { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Value { get; set; } = string.Empty;
}
