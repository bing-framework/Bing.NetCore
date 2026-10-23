namespace Bing.Samples.SqlUnitOfWork.Infrastructure.Database;

/// <summary>
/// 当前宿主选择的存储桥接模式。
/// </summary>
public sealed record SqlUnitOfWorkSampleOptions(bool EfBridgeEnabled, bool CapMySqlMode = false,
    string? OrdersConnectionString = null, string? ReportingConnectionString = null,
    string? RabbitMqConnectionString = null, string? MySqlAdminConnectionString = null,
    string? ProcessEvidencePath = null)
{
    public bool OperationsEnabled { get; init; }
}
