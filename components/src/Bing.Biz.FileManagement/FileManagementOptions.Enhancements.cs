namespace Bing.Biz.FileManagement;

public sealed partial class FileContainerPolicy
{
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AllowedContentTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>扩展名到文件头十六进制前缀；例如 .pdf → 25504446。未设置时不强制文件头。</summary>
    public Dictionary<string, string> FileHeaders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool RequireMalwareScan { get; set; }
}
public sealed class FileStoreIoLimits
{
    /// <summary>0 不限；开启后多个应用实例共用元数据库中的槽。</summary>
    public int LocalParts { get; set; }
    public int BackgroundOperations { get; set; }
}
public sealed class FileClamAvOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 3310;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);
    public long MaxStreamLength { get; set; } = 25L * 1024 * 1024;
}
public sealed partial class FileManagementOptions
{
    public FileUploadAdmission Admission { get; set; } = new();
    public Dictionary<string, FileStoreIoLimits> StoreIoLimits { get; set; } = new(StringComparer.Ordinal);
    public TimeSpan ResourceLease { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan EventLease { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan EventHandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public FileClamAvOptions ClamAv { get; set; } = new();

    internal void ValidateEnhancements()
    {
        if (MaxEventDeliveryAttempts is < 1 or > 100 || HistoryRetentionDays is < 7 or > 3650 ||
            Admission == null || Admission.TenantSessions < 0 || Admission.TenantBytes < 0 || Admission.UserSessions < 0 || Admission.UserBytes < 0 ||
            ResourceLease < TimeSpan.FromSeconds(10) || ResourceLease > TimeSpan.FromMinutes(5) ||
            EventLease < TimeSpan.FromSeconds(10) || EventLease > TimeSpan.FromMinutes(5) ||
            EventHandlerTimeout < TimeSpan.FromSeconds(1) || EventHandlerTimeout > TimeSpan.FromMinutes(10) ||
            ClamAv.Port is < 1 or > 65535 || ClamAv.Timeout <= TimeSpan.Zero || ClamAv.Timeout > TimeSpan.FromHours(2) || ClamAv.MaxStreamLength is < 1 or > int.MaxValue)
            throw new ArgumentException("文件增强配置越界。");
        foreach (var limits in StoreIoLimits.Values)
            if (limits.LocalParts is < 0 or > 1024 || limits.BackgroundOperations is < 0 or > 128) throw new ArgumentException("存储并发限制越界。");
        foreach (var policy in Containers.Values)
        {
            if (policy.AllowedExtensions.Any(x => !ValidExtension(x)) || policy.AllowedContentTypes.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 256 || x.Any(char.IsControl)))
                throw new ArgumentException("文件类型策略无效。");
            foreach (var header in policy.FileHeaders)
            {
                if (!ValidExtension(header.Key) || string.IsNullOrWhiteSpace(header.Value) || header.Value.Length > 8192 || header.Value.Length % 2 != 0)
                    throw new ArgumentException("文件头策略无效。");
                try { _ = Convert.FromHexString(header.Value); } catch (FormatException) { throw new ArgumentException("文件头必须是十六进制。"); }
            }
            if (policy.RequireMalwareScan && string.IsNullOrWhiteSpace(ClamAv.Host)) throw new ArgumentException("要求病毒扫描的容器必须配置扫描服务。");
            if (policy.RequireMalwareScan && policy.MaxFileLength > ClamAv.MaxStreamLength) throw new ArgumentException("扫描容器上限不得超过扫描流上限。");
            if (System.Text.Json.JsonSerializer.Serialize(policy).Length > 32768) throw new ArgumentException("内容策略过大。");
        }
    }
    private static bool ValidExtension(string value) => value.Length is > 1 and <= 32 && value[0] == '.' && value.Skip(1).All(char.IsLetterOrDigit);
}
